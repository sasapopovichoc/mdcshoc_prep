using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HOCMessengerClient
{
	class HOCMessengerClient
	{
		string connectionString = @"Server=tcp:kzkogjfm75.database.windows.net,1433;Database=hocm;User ID=sasapopo@kzkogjfm75;Password=Password1;Trusted_Connection=False;Encrypt=True;Connection Timeout=30;";
		
		protected const string LOGIN = "_login";
		protected const string LOGOUT = "_logout";
		protected const string ADDUSER = "_adduser";
		protected const string ABOUT = "_about";
		protected List<string> HELP = new List<string> {"_help", "/?", "-?"};

		protected int? userid = null;
		protected string username = null;

		public void ProcessCommand(string command)
		{
			if (command.Equals(LOGIN))
			{
				Login();
			}
			else if (command.Equals(LOGOUT))
			{
				Logout();
			}
			else if (command.Equals(ADDUSER))
			{
				AddUser();
			}
			else if (HELP.Contains(command))
			{
				ShowHelp();
			}
			else if (ABOUT.Equals(command))
			{
				ShowAbout();
			}
			else
			{
				SendMessage(command);
			}
		}

		public string GetUsername()
		{
			if (username == null)
			{
				return "?";
			}
			else
			{
				return username;
			}
		}

		private void Logout()
		{
			// Set userid and username to null;
			//
			// TODO: Missing implementation.
			string uname = (username == null ? "?" : username);
			username = null;
			userid = null;

			// Put entry in eventLog table that user 'Username' has logged out.
			//
			using (SqlConnection conn = new SqlConnection(connectionString))
			{
				conn.Open();
				using (SqlCommand command = new SqlCommand())
				{
					command.Connection = conn;
					command.CommandText = String.Format("INSERT INTO eventLog values(getutcdate(), 'User {0} has logged out.')", uname);
					command.ExecuteNonQuery();
				}
			}
		}

		private void Login()
		{
			// Ask for username and password.
			// Check if that combination of username and password exist in the database.
			// If yes, set variables userid and username to the right values.
			//
			Console.WriteLine("Enter username...");
			string uname = Console.ReadLine();
			Console.WriteLine("Enter password...");
			string password = Console.ReadLine();

			using (SqlConnection conn = new SqlConnection(connectionString))
			{
				conn.Open();
				using (SqlCommand command = new SqlCommand())
				{
					command.Connection = conn;
					command.CommandText = String.Format("select username, userid from users where username = '{0}' and password = '{1}'", uname, password);
					using (SqlDataReader reader = command.ExecuteReader())
					{
						if (reader.Read())
						{
							int id = reader.GetInt32(1);
							userid = id;
							username = uname;

							Console.WriteLine("Wellcome " + username + "!");
						}
						else
						{
							Console.WriteLine("Login failed. Bad username or password");
						}
					}
				}
			}
		}

		private void AddUser()
		{
			// Ask for new username and password.
			// Create new user by inserting row in the messenger database.
			//
			Console.WriteLine("Enter new username");
			string newusername = Console.ReadLine();
			Console.WriteLine("Enter password");
			string password = Console.ReadLine();
			Console.WriteLine("Enter email");
			string email = Console.ReadLine();

			// Put entry in eventLog table that user 'Username' has logged out.
			//
			using (SqlConnection conn = new SqlConnection(connectionString))
			{
				conn.Open();

				using (SqlCommand command = new SqlCommand())
				{
					command.Connection = conn;
					command.CommandText = String.Format("select count(*) from users where username = '{0}'", newusername);
					int count = Int32.Parse(command.ExecuteScalar().ToString());

					if (count == 0)
					{
						using (SqlCommand command2 = new SqlCommand())
						{
							command2.Connection = conn;
							command2.CommandText = String.Format("INSERT INTO users values('{0}', '{1}', '{2}')", newusername, password, email);
							command2.ExecuteNonQuery();
						}
						Console.WriteLine("User added.");
					}
					else
					{
						Console.WriteLine("Username already exists. Chose new one and try again.");
					}
				}
			}

		}

		private void ShowHelp()
		{
			using (TextReader tr = new StreamReader("Help.txt"))
			{
				string helpContent = tr.ReadToEnd();
				Console.WriteLine(helpContent);
			}

			// Put in eventLog table info that user 'username' asked for help.
			//
			string uname = username == null ? "?" : username;
			using (SqlConnection conn = new SqlConnection(connectionString))
			{
				conn.Open();
				using (SqlCommand command = new SqlCommand())
				{
					command.Connection = conn;
					command.CommandText = String.Format("INSERT INTO eventLog values(getutcdate(), 'User {0} asked for help.')", uname);
					command.ExecuteNonQuery();
				}
			}
		}

		private void ShowAbout()
		{
			// Show text from file About.txt
			//
			using (TextReader tr = new StreamReader("About.txt"))
			{
				string aboutContent = tr.ReadToEnd();
				Console.WriteLine(aboutContent);
			}

			// Put in eventLog table info that user executed About command.
			//
			string uname = username == null ? "?" : username;
			using (SqlConnection conn = new SqlConnection(connectionString))
			{
				conn.Open();
				using (SqlCommand command = new SqlCommand())
				{
					command.Connection = conn;
					command.CommandText = String.Format("INSERT INTO eventLog values(getutcdate(), 'User {0} asked for about.')", uname);
					command.ExecuteNonQuery();
				}
			}
		}

		private void SendMessage(string message)
		{
			string toUsername = null;
			int? toUserId = null;

			ExtractToUserFromMessage(ref message, out toUsername, out toUserId);

			// Send message by inserting row into messenger database.
			// Row should contain current UTC time, fromUserid, toUserId and message text
			//
			// Implementation below always sets columns fromUserid, toUserId to be null.
			// This should be fixed.
			//
			using (SqlConnection openCon = new SqlConnection(connectionString))
			{
				string insertMessageQuery = String.Format(
					"INSERT INTO messages VALUES ({0}, {1}, '{2}', getutcdate())",
					userid == null ? "null" : userid.ToString(),
					toUserId == null ? "null" : toUserId.ToString(),
					message);

				using (SqlCommand insertMessageCommand = new SqlCommand(insertMessageQuery))
				{
					insertMessageCommand.Connection = openCon;
					openCon.Open();

					insertMessageCommand.ExecuteNonQuery();
				}
			}
		}

		private void ExtractToUserFromMessage(ref string message, out string toUsername, out int? toUserId)
		{
			toUsername = null;
			toUserId = null;

			// If message looks like this: "John123,,Do you have 5 minutes?"
			// that means that message text "Do you have 5 minutes?"
			// should be sent to user with username John123.
			// In that case find userId for John123 and set that value to: toUserId.
			// Also, set toUsername to: John123
			// If message does not contain sequence ",," that means to whom message should be send is not defined.
			//
			if (message.Contains(",,"))
			{
				string uname = message.Substring(0, message.IndexOf(",,"));
				int? uid = null;

				using (SqlConnection conn = new SqlConnection(connectionString))
				{
					conn.Open();
					using (SqlCommand command = new SqlCommand())
					{
						command.Connection = conn;
						command.CommandText = String.Format("select userid from users where username = '{0}'", uname);
						try
						{
							uid = Int32.Parse(command.ExecuteScalar().ToString());
						}
						catch (SqlException sqlException)
						{
							Console.WriteLine("Error: " + sqlException.Message);
						}
					}
				}

				if (uid != null)
				{
					toUsername = uname;
					toUserId = uid;

					message = message.Substring(message.IndexOf(",,") + 2);
				}
			}
		}
	}
}
