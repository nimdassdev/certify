﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Certify.Core.Management.Access;
using Certify.Datastore.Postgres;
using Certify.Datastore.SQLServer;
using Certify.Management;
using Certify.Models.Config.AccessControl;
using Certify.Providers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Certify.Core.Tests.DataStores
{
    [TestClass]
    public class AccessControlDataStoreTests
    {
        private string _storeType = "sqlite";
        private const string TEST_PATH = "Tests\\credentials";

        public static IEnumerable<object[]> TestDataStores
        {
            get
            {
                return new[]
                {
                    new object[] { "sqlite" },
                    //new object[] { "postgres" },
                    //new object[] { "sqlserver" }
                };
            }
        }

        private IAccessControlStore GetStore(string storeType = null)
        {
            if (storeType == null)
            {
                storeType = _storeType;
            }

            if (storeType == "sqlite")
            {
                return new SQLiteAccessControlStore(storageSubfolder: TEST_PATH);
            }
            /* else if (storeType == "postgres")
             {
                 return new PostgresCredentialStore(Environment.GetEnvironmentVariable("CERTIFY_TEST_POSTGRES"));
             }
             else if (storeType == "sqlserver")
             {
                 return new SQLServerCredentialStore(Environment.GetEnvironmentVariable("CERTIFY_TEST_SQLSERVER"));
             }*/
            else
            {
                throw new ArgumentOutOfRangeException(nameof(storeType), "Unsupported store type " + storeType);
            }
        }

        [TestMethod]
        [DynamicData(nameof(TestDataStores))]
        public async Task TestStoreSecurityPrinciple(string storeType)
        {
            var store = GetStore(storeType ?? _storeType);

            var sp = new SecurityPrinciple
            {
                Email = "test@test.com",
                PrincipleType = SecurityPrincipleType.User,
                Username = "test",
                Provider = StandardProviders.INTERNAL
            };

            try
            {
                await store.Add(nameof(SecurityPrinciple), sp);

                var list = await store.GetItems<SecurityPrinciple>(nameof(SecurityPrinciple));

                Assert.IsTrue(list.Any(l => l.Id == sp.Id), "Security Principle retrieved");
            }
            finally
            {
                // cleanup
                await store.Delete<SecurityPrinciple>(nameof(SecurityPrinciple), sp.Id);
            }
        }

        [TestMethod]
        [DynamicData(nameof(TestDataStores))]
        public async Task TestStoreRole(string storeType)
        {
            var store = GetStore(storeType ?? _storeType);

            var role1 = new Role("test", "Test Role", "A test role");
            var role2 = new Role("test2", "Test Role 2", "A test role 2");

            try
            {
                await store.Add(nameof(Role), role1);
                await store.Add(nameof(Role), role2);

                var item = await store.Get<Role>(nameof(Role), role1.Id);

                Assert.IsTrue(item.Id == role1.Id, "Role retrieved");
            }
            finally
            {
                // cleanup
                await store.Delete<Role>(nameof(Role), role1.Id);
                await store.Delete<Role>(nameof(Role), role2.Id);
            }
        }

        [TestMethod]
        public void TestStorePasswordHashing()
        {
            var store = GetStore(_storeType);
            var access = new AccessControl(null, store);

            var firstHash = access.HashPassword("secret");

            Assert.IsNotNull(firstHash);

            Assert.IsTrue(access.IsPasswordValid("secret", firstHash));
        }

        [TestMethod]
        [DynamicData(nameof(TestDataStores))]
        public async Task TestStoreGeneralAccessControl(string storeType)
        {

            var store = GetStore(storeType ?? _storeType);

            var access = new AccessControl(null, store);

            var adminSp = new SecurityPrinciple
            {
                Id = "admin_01",
                Email = "admin@test.com",
                Description = "Primary test admin",
                PrincipleType = SecurityPrincipleType.User,
                Username = "admin01",
                Provider = StandardProviders.INTERNAL
            };

            var consumerSp = new SecurityPrinciple
            {
                Id = "dev_01",
                Email = "dev_test01@test.com",
                Description = "Consumer test",
                PrincipleType = SecurityPrincipleType.User,
                Username = "dev01",
                Password = "oldpassword",
                Provider = StandardProviders.INTERNAL
            };

            try
            {
                // add first admin security principle, bypass role check as there is no user to check yet

                await access.AddSecurityPrinciple(adminSp.Id, adminSp, bypassIntegrityCheck: true);

                // add second security principle, enforcing role checks for calling user
                await access.AddSecurityPrinciple(adminSp.Id, consumerSp);

                var list = await access.GetSecurityPrinciples(adminSp.Id);

                Assert.IsTrue(list.Any());

                // get updated sp so that password is hashed for comparison check
                consumerSp = await access.GetSecurityPrinciple(adminSp.Id, consumerSp.Id);

                Assert.IsTrue(access.IsPasswordValid("oldpassword", consumerSp.Password));

                var updated = await access.UpdateSecurityPrinciplePassword(adminSp.Id, new Models.API.SecurityPrinciplePasswordUpdate
                {
                    SecurityPrincipleId = consumerSp.Id,
                    Password = "oldpassword",
                    NewPassword = "newpassword"
                });

                Assert.IsTrue(updated, "SP password should have been updated OK");

                consumerSp = await access.GetSecurityPrinciple(adminSp.Id, consumerSp.Id);

                Assert.IsFalse(access.IsPasswordValid("oldpassword", consumerSp.Password), "Old password should no longer be valid");

                Assert.IsTrue(access.IsPasswordValid("newpassword", consumerSp.Password), "New password should be valid");
            }
            finally
            {
                await access.DeleteSecurityPrinciple(adminSp.Id, consumerSp.Id);
                await access.DeleteSecurityPrinciple(adminSp.Id, adminSp.Id, allowSelfDelete: true);
            }
        }
    }
}
