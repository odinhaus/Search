using MongoRepository;

namespace Suffuz.Identity
{
    using Suffuz.Identity.Entities;
    using Suffuz.Identity.Models;
    using Microsoft.AspNet.Identity;
    using MongoDB.Driver.Builders;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using AspNet.Identity.MongoDB;

    public class AuthRepository
    {
        private readonly IMongoContext mongoContext;
        private readonly ApplicationUserManager userManager;
        private readonly IRepository<Customer> _customerRepository; 

        public AuthRepository(IMongoContext mongoContext, ApplicationUserManager userManager, IRepository<Customer> customerRepository)
        {
            this.mongoContext = mongoContext;
            this.userManager = userManager;
            _customerRepository = customerRepository;
        }
        

        public Customer RegisterCustomer(Customer customer)
        {
            // TODO: make sure there are no duplicate prefixes
//            if (_customerRepository.Any(c => c.Prefix == customer.Prefix))

            var result = _customerRepository.Add(customer);

            return result;
        }

        public async Task<IdentityResult> RegisterUser(UserModel userModel)
        {
            var customer = _customerRepository.SingleOrDefault(c => c.Prefix == userModel.CustomerPrefix);

            if (customer == null)
                customer = _customerRepository.Add(new Customer
                {
                    Name = userModel.CustomerPrefix,
                    Prefix = userModel.CustomerPrefix
                });
            //                return IdentityResult.Failed("Invalid Prefix");

            var user = new User
            {
                UserName = userModel.UserName,
                CustomerId = customer.Id,

            };

            var result = await userManager.CreateAsync(user, userModel.Password);

            return result;
        }

        public async Task<User> FindUser(string userName, string password)
        {
            User user = await userManager.FindAsync(userName, password);

            return user;
        }

        public Client FindClient(string clientId)
        {
            var query = Query<Client>.EQ(c => c.Id, clientId);

            var client = mongoContext.Clients.Find(query).SetLimit(1).FirstOrDefault();

            return client;
        }

        public Customer FindCustomer(string customerId)
        {
            var query = Query<Customer>.EQ(c => c.Id, customerId);

            return _customerRepository.FirstOrDefault(c => c.Id == customerId);
        }

        public async Task<bool> AddRefreshToken(RefreshToken token)
        {
            var query = Query.And(
                Query<RefreshToken>.EQ(r => r.Subject, token.Subject),
                Query<RefreshToken>.EQ(r => r.ClientId, token.ClientId));

            var existingToken = mongoContext.RefreshTokens.Find(query).SetLimit(1).SingleOrDefault();

            if (existingToken != null)
            {
                var result = await RemoveRefreshToken(existingToken);
            }

            mongoContext.RefreshTokens.Insert(token);

            return true;
        }

        public Task<bool> RemoveRefreshToken(string refreshTokenId)
        {
            var query = Query<RefreshToken>.EQ(r => r.Id, refreshTokenId);

            var writeConcernResult = mongoContext.RefreshTokens.Remove(query);

            return Task.FromResult(writeConcernResult.DocumentsAffected == 1);
        }

        public async Task<bool> RemoveRefreshToken(RefreshToken refreshToken)
        {
            return await RemoveRefreshToken(refreshToken.Id);
        }

        public Task<RefreshToken> FindRefreshToken(string refreshTokenId)
        {
            var query = Query<RefreshToken>.EQ(r => r.Id, refreshTokenId);

            var refreshToken = mongoContext.RefreshTokens.Find(query).SetLimit(1).FirstOrDefault();

            return Task.FromResult(refreshToken);
        }

        public List<RefreshToken> GetAllRefreshTokens()
        {
            return mongoContext.RefreshTokens.FindAll().ToList();
        }

        public async Task<IdentityUser> FindAsync(UserLoginInfo loginInfo)
        {
            IdentityUser user = await userManager.FindAsync(loginInfo);

            return user;
        }

        public async Task<IdentityResult> CreateAsync(User user)
        {
            var result = await userManager.CreateAsync(user);

            return result;
        }

        public async Task<IdentityResult> AddLoginAsync(string userId, UserLoginInfo login)
        {
            var result = await userManager.AddLoginAsync(userId, login);

            return result;
        }
    }
}