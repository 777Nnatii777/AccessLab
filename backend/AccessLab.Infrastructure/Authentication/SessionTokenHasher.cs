using System.Security.Cryptography;
using System.Text;

namespace AccessLab.Infrastructure.Authentication;

public static class SessionTokenHasher
{
    public static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
