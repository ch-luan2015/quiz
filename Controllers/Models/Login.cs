namespace dmc_auth.Controllers.Models
{
  public class Login
  {
    public string username { get; set; }
    public string password { get; set; }
    public string login_challenge { get; set; }
  }
}