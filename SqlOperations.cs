using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
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

        public static void SetUserState(Int64 userID, StaticStuff.UserState state, NpgsqlDataSource dataSource)
        {
            var cmd = dataSource.CreateCommand($"UPDATE usertable SET state = {(int)state} WHERE id = {userID}");
            cmd.ExecuteNonQuery();
        }

        public static void CreateUser(Int64 userID, NpgsqlDataSource dataSource)
        {
            Console.WriteLine($"INSERT INTO usertable (id, state) VALUES ({userID}, {(int)StaticStuff.UserState.NoAuth})");
            var cmd = dataSource.CreateCommand($"INSERT INTO usertable (id, state) VALUES ({userID}, {(int)StaticStuff.UserState.NoAuth})");
            cmd.ExecuteNonQuery();
        }

        public static void SetUserToken(Int64 userID, string token, NpgsqlDataSource dataSource)
        {
            var cmd = dataSource.CreateCommand($"UPDATE usertable SET token = {token} WHERE id = {userID}");
            cmd.ExecuteNonQuery();
        }

        public static string GetUserToken(Int64 userID, NpgsqlDataSource dataSource) 
        {
            var cmd = dataSource.CreateCommand($"SELECT token FROM usertable WHERE id = {userID}");
            var reader = cmd.ExecuteReader();
            return reader.GetString(0);
        }

        public static void SetUserChatID(Int64 userID, long chatID, NpgsqlDataSource dataSource) 
        {
            var cmd = dataSource.CreateCommand($"UPDATE usertable SET chatID = {chatID} WHERE id = {userID}");
            cmd.ExecuteNonQuery();
        }

        public static long GetUserChatID(Int64 userID, NpgsqlDataSource dataSource)
        {
            var cmd = dataSource.CreateCommand($"SELECT chatID FROM usertable WHERE id = {userID}");
            var reader = cmd.ExecuteReader();
            return reader.GetInt64(0);
        }
    }
}
