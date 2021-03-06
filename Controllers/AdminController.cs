using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using dmc_auth;
using dmc_auth.Controllers.Models;
using dmc_auth.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CleanArchitecture.Web.Api
{
  [Route("api/[controller]")]
  [ApiController]
  public class AdminController : ControllerBase
  {
    //private readonly ApplicationDbContext _context;    
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;

    public AdminController(UserManager<ApplicationUser> userManager, RoleManager<ApplicationRole> roleManager)
    {
      _userManager = userManager;
      _roleManager = roleManager;
    }

    [HttpGet]
    [Route("users")]
    public async Task<ActionResult<List<UserResponse>>> AccountList([FromQuery] UserPagingQuery model)
    {
      var query = _userManager.Users
      .Include(u => u.Employee)
      .ThenInclude(u => u.Department)
      .AsQueryable();
      var users = await model.BuildQuery(query).ToListAsync();
      var res = new List<UserResponse>();
      foreach (var user in users)
      {
        var roles = await _userManager.GetRolesAsync(user);
        var resItem = new UserResponse(user, roles);
        res.Add(resItem);
      }
      return res;
    }

    [HttpPost]
    [Route("user")]
    public async Task<ActionResult<UserResponse>> CreateUser(CreateUserModel model)
    {
      var user = new ApplicationUser
      {
        UserName = model.Username,
        Email = model.Email,
        LockoutEnd = model.LockoutEnd,
        LockoutEnabled = model.LockoutEnable ?? false
      };
      var result = await _userManager.CreateAsync(user, model.Password);
      if (result.Succeeded)
      {
        var addRolesResult = await _userManager.AddToRolesAsync(user, model.Roles);
        if (addRolesResult.Succeeded)
        {
          var response = new UserResponse(user, model.Roles);
          return Ok(response);
        }
        return ResponseIdentityResultError(result);
      }
      return ResponseIdentityResultError(result);
    }

    [HttpGet]
    [Route("user/{id}")]
    public async Task<ActionResult<UserResponse>> Detail(string id)
    {
      var user = await _userManager.Users.Include(u => u.Employee).ThenInclude(u => u.Department).FirstOrDefaultAsync(u => u.Id == id);

      if (user == null) return NotFound();
      var roles = await _userManager.GetRolesAsync(user);
      return new UserResponse(user, roles);
    }

    [HttpPut]
    [Route("user/{id}")]
    public async Task<ActionResult<UserResponse>> Update(string id, EditUserModel model)
    {
      var user = await _userManager.FindByIdAsync(id);
      if (user == null) return NotFound();
      user.Email = model.Email;
      user.LockoutEnabled = model.LockoutEnable ?? false;
      user.LockoutEnd = model.LockoutEnd;
      user.Image = model.Image;
      user.PhoneNumber = model.Phone;
      user.Fullname = model.Fullname;
      user.Nickname = model.Nickname;
      var result = await _userManager.UpdateAsync(user);
      if (result.Succeeded) return new UserResponse(user, new List<string>());
      return ResponseIdentityResultError(result);
    }

    [HttpPost]
    [Route("user/{id}/lock")]
    public async Task<IActionResult> Lock(string id)
    {
      var user = await _userManager.FindByIdAsync(id);
      var identityResult = await _userManager.SetLockoutEnabledAsync(user, true);
      if (!identityResult.Succeeded)
      {
        return ResponseIdentityResultError(identityResult);
      }
      identityResult = await _userManager.SetLockoutEndDateAsync(user, new DateTime(2100, 1, 1));
      if (!identityResult.Succeeded)
      {
        return ResponseIdentityResultError(identityResult);
      }
      return Ok();
    }

    [HttpPost]
    [Route("user/{id}/unlock")]
    public async Task<IActionResult> Unlock(string id)
    {
      var user = await _userManager.FindByIdAsync(id);
      var identityResult = await _userManager.SetLockoutEndDateAsync(user, DateTime.UtcNow);
      if (!identityResult.Succeeded)
      {
        return ResponseIdentityResultError(identityResult);
      }
      return Ok();
    }

    [HttpPost]
    [Route("user/{id}/reset")]
    public async Task<IActionResult> ResetPassword(string id)
    {
      var user = await _userManager.FindByIdAsync(id);
      if (user == null)
      {
        // Don't reveal that the user does not exist
        return NotFound();
      }
      var token = await _userManager.GeneratePasswordResetTokenAsync(user);
      var password = GenerateRandomPassword(_userManager.Options.Password);
      var result = await _userManager.ResetPasswordAsync(user, token, password);

      if (result.Succeeded)
        return Ok(new { password });
      return ResponseIdentityResultError(result);
    }

    [HttpGet]
    [Route("user/{id}/roles")]
    public async Task<ActionResult<List<RoleResponse>>> Roles(string id)
    {
      var user = await _userManager.FindByIdAsync(id);
      if (user == null)
        return BadRequest();

      var roleNames = await _userManager.GetRolesAsync(user);
      var roles = await _roleManager.Roles.Where(e => roleNames.Contains(e.Name)).ToListAsync();
      return roles.Select(role => new RoleResponse(role)).ToList();
    }

    [HttpGet]
    [Route("roles")]
    public async Task<ActionResult<List<RoleResponse>>> GetAllRoles()
    {
      var roles = await _roleManager.Roles.ToListAsync();
      return roles.Select(role => new RoleResponse(role)).ToList();
    }

    [HttpPut]
    [Route("user/{userId}/role/{role}")]
    public async Task<IActionResult> GrantUserRole(string userId, string role)
    {
      var user = await _userManager.FindByIdAsync(userId);
      var identityResult = await _userManager.AddToRoleAsync(user, role);
      if (identityResult.Succeeded)
        return Ok();
      return ResponseIdentityResultError(identityResult);
    }

    [HttpDelete]
    [Route("user/{userId}/role/{role}")]
    public async Task<IActionResult> RevokeUserRole(string userId, string role)
    {
      var user = await _userManager.FindByIdAsync(userId);
      var identityResult = await _userManager.RemoveFromRoleAsync(user, role);
      if (identityResult.Succeeded)
        return Ok();
      return ResponseIdentityResultError(identityResult);
    }

    private BadRequestObjectResult ResponseIdentityResultError(IdentityResult identityResult)
    {
      var messages = identityResult.Errors.Select(u => $"{u.Code}: {u.Description}").ToList();
      var errResponse = new ErrorResponse();
      if (messages.Count > 0)
        errResponse.Message = messages[0];
      errResponse.Messages = messages;
      return BadRequest(errResponse);
    }

    private string GenerateRandomPassword(PasswordOptions opts = null)
    {
      if (opts == null) opts = new PasswordOptions()
      {
        RequiredLength = 8,
        RequiredUniqueChars = 4,
        RequireDigit = true,
        RequireLowercase = true,
        RequireNonAlphanumeric = true,
        RequireUppercase = true
      };

      string[] randomChars = new[] {
        "ABCDEFGHJKLMNOPQRSTUVWXYZ",    // uppercase 
        "abcdefghijkmnopqrstuvwxyz",    // lowercase
        "0123456789",                   // digits
        "!@$?_-"                        // non-alphanumeric
    };
      Random rand = new Random(Environment.TickCount);
      List<char> chars = new List<char>();

      if (opts.RequireUppercase)
        chars.Insert(rand.Next(0, chars.Count),
            randomChars[0][rand.Next(0, randomChars[0].Length)]);

      if (opts.RequireLowercase)
        chars.Insert(rand.Next(0, chars.Count),
            randomChars[1][rand.Next(0, randomChars[1].Length)]);

      if (opts.RequireDigit)
        chars.Insert(rand.Next(0, chars.Count),
            randomChars[2][rand.Next(0, randomChars[2].Length)]);

      if (opts.RequireNonAlphanumeric)
        chars.Insert(rand.Next(0, chars.Count),
            randomChars[3][rand.Next(0, randomChars[3].Length)]);

      for (int i = chars.Count; i < opts.RequiredLength
          || chars.Distinct().Count() < opts.RequiredUniqueChars; i++)
      {
        string rcs = randomChars[rand.Next(0, randomChars.Length)];
        chars.Insert(rand.Next(0, chars.Count),
            rcs[rand.Next(0, rcs.Length)]);
      }

      return new string(chars.ToArray());
    }
  }
}