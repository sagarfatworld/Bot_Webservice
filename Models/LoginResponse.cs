namespace Botatwork_in_Livechat.Models
{
    public class LoginResponse
    {
        public string Status { get; set; }
        public string Message_Code { get; set; }
        public string Message { get; set; }
        public TokenData Data { get; set; }
    }

    public class TokenData
    {
        public string Access_Token { get; set; }
        public string Refresh_Token { get; set; }
    }

    public class UserSession
    {
        public string AccessToken { get; set; }
        public string RefreshToken { get; set; }
    }
}
