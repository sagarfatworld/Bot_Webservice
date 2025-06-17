namespace Botatwork_in_Livechat.Models
{
    public class UserInfoResponse
    {
        public string Status { get; set; }
        public string Message_Code { get; set; }
        public string Message { get; set; }
        public UserData Data { get; set; }
    }

    public class UserData
    {
        public string Id { get; set; }
        public string Email { get; set; }
        public string Client_Id { get; set; }
        public string Client_Name { get; set; }
        public List<ClientInfo> Clients { get; set; }
    }

    public class ClientInfo
    {
        public string Client_Id { get; set; }
        public string Client_Name { get; set; }
    }
}
