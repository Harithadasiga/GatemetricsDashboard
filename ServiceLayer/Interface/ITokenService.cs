namespace GatemetricsData.ServiceLayer.Interface
{
    public interface ITokenService
    {
        string GenerateToken(string username);
    }
}