using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Npgsql;

namespace telegramvkbridge
{
    internal static class SqlOperations
    {
        public static StaticStuff.UserState GetUserState(Int64 userId, NpgsqlDataSource dataSource)
        {
            var cmd = dataSource.CreateCommand($"SELECT state FROM usertable WHERE id = {userId}");
            var reader = cmd.ExecuteReader();
            if (reader.Read()) return (StaticStuff.UserState)reader.GetInt32(0);
            else
            {
                CreateUser(userId, dataSource);
                Console.WriteLine("new user was attempted to be created");
                return StaticStuff.UserState.NoAuth;
            }
        }

        public static void SetUserState(Int64 userId, StaticStuff.UserState state, NpgsqlDataSource dataSource)
        {
            var cmd = dataSource.CreateCommand($"UPDATE usertable SET state = {(int)state} WHERE id = {userId}");
            cmd.ExecuteNonQuery();
        }

        public static void CreateUser(Int64 userId, NpgsqlDataSource dataSource)
        {
            var cmd = dataSource.CreateCommand($"INSERT INTO usertable (id, state) VALUES ({userId}, {(int)StaticStuff.UserState.NoAuth}");
        }

        public static void SetUserToken(Int64 userId, string token, NpgsqlDataSource dataSource)
        {
            var cmd = dataSource.CreateCommand($"UPDATE usertable SET token = {token} WHERE id = {userId}");
            cmd.ExecuteNonQuery();
        }
    }
}
