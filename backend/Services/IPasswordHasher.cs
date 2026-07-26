namespace Storefront.Api.Services
{
    public interface IPasswordHasher
    {
        /// <summary>Produces a self-describing digest safe to persist.</summary>
        string Hash(string password);

        /// <summary>
        /// Verifies a password against a stored digest in constant time with
        /// respect to the digest contents.
        /// </summary>
        bool Verify(string password, string encodedHash);
    }
}
