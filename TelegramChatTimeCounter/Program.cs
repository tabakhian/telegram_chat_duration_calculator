using System;
using OpenTl.ClientApi;
using OpenTl.ClientApi.MtProto.Exceptions;
using OpenTl.Schema;
using System.Linq;
using OpenTl.Schema.Messages;
using OpenTl.Schema.Channels;
using System.Threading.Tasks;
using System.Net;
using System.Collections.Generic;
using LinqExtensions;
using System.IO;

namespace TelegramChatTimeCounter
{
	public class Program
	{
		public static void Main(string[] args)
		{
			try
			{
				bool boolSuccessOfOperation = Operation().GetAwaiter().GetResult();
				if (boolSuccessOfOperation)
				{
					Console.WriteLine("\nOperation Finished Successfuly");
				}
				else
				{
					Console.WriteLine("\nOperation Failed");
				}
				Console.WriteLine("\nFinish Processing");
			}
			catch (Exception)
			{
				Console.WriteLine("\nOperation Failed");
			}
			string strExit = Console.ReadLine();
		}

		public static async Task<IClientApi> CreateTelegramClient()
		{
			Socks5ProxyConfig ProxyCreate(string ip, int port, string user = null, string pass = null)
			{
				Socks5ProxyConfig proxyConfig = null;

				if (!string.IsNullOrEmpty(ip))
				{
					proxyConfig = new Socks5ProxyConfig
					{
						Endpoint = new IPEndPoint(IPAddress.Parse(ip), port),
						Password = string.IsNullOrEmpty(pass) ? null : pass,
						Username = string.IsNullOrEmpty(user) ? null : user,
					};
				}

				return proxyConfig;
			}

			var settings = new FactorySettings
			{
				AppHash = "",
				AppId = 23838383,
				ServerAddress = "149.154.167.50",
				ServerPublicKey = @"-----BEGIN RSA PUBLIC KEY-----
MIIBCgKCAQEAwVACPi9w23mF3tBkdZz+zwrzKOaaQdr01vAbU4E1pvkfj4sqDsm6
lyDONS789shrhrVoD/xCS9Y0hkkC3gtL1tSfTlgCMOOul9lcifefexlEKzwKENj1Yz/s7daS
an9tqw3bfUV/nqgbhGX81v/+7RFAEd+RwFnK7ajtht+XYl9sluzHRyVVaTTveB2GazTw
Efzk2DWgkBluml8OREmvfraX3bkHZJTKX4EQSjBbbdJ2ZXIsRrYOXfaA+xayEGB+
8hdlLmAjfefbCVfaigxX0CDqWeRhrhr1yFL9kwd9P0NsZRPsmoqVwMbMu7mStFai6aIhc3n
Slv8kg9qv1m6XHVfefeQY3PnEw+QQtqSIXklHwIDAQAB
-----END RSA PUBLIC KEY-----",
				ServerPort = 443,
				//ProxyConfig = ProxyCreate("127.0.0.1",1080),
				SessionTag = "session",
				Properties = new ApplicationProperties
				{
					AppVersion = "1.0.0",
					DeviceModel = "PC",
					LangCode = "en",
					LangPack = "tdesktop",
					SystemLangCode = "en",
					SystemVersion = "Win 10 Pro"
				}
			};

			IClientApi clientApi = await ClientFactory.BuildClientAsync(settings).ConfigureAwait(false);

			if (!clientApi.AuthService.CurrentUserId.HasValue)
			{
				Console.WriteLine("Please Enter Your Telegram Account Number To Signin:");
				var phone = Console.ReadLine();

				var sentCode = await clientApi.AuthService.SendCodeAsync(phone).ConfigureAwait(false);

				Console.WriteLine("\nEnter Telegram Verification Code:");
				var code = Console.ReadLine();

				TUser user;
				try
				{
					user = await clientApi.AuthService.SignInAsync(phone, sentCode, code).ConfigureAwait(false);
				}
				catch (PhoneCodeInvalidException)
				{
					Console.WriteLine("\nTelegram Communication Account is Invalid!!!");
				}
				catch (CloudPasswordNeededException)
				{
					Console.WriteLine("\nYou Have Telegram Two-Step Auth Password And This App Cant Handle That!!! Please Turn Of Your Telegram Cloud Password To Use This App And After That you Can Turn it On again!");
				}
			}
			clientApi.KeepAliveConnection();

			return (clientApi);
		}

		public static IClientApi TelegramClientGet()
		{
			if (_telegramClient == null)
			{
				lock (new object())
				{				
					_telegramClient = CreateTelegramClient().GetAwaiter().GetResult();
				}
			}
			return (_telegramClient);
		}
		public static IClientApi TelegramClientReset()
		{
			_telegramClient = null;

			if (_telegramClient == null)
			{
				lock (new object())
				{
					if (_telegramClient == null)
					{
						lock (new object())
						{
							_telegramClient = TelegramClientGet();
						}
					}
				}
			}
			return (_telegramClient);
		}
		private static IClientApi _telegramClient { get; set; }

		public static async Task<GetChatHistoryMessagesResponse> GetChatHistoryMessages(TInputPeerUser targetUserPeer, int messageLimit, int offset)
		{
			GetChatHistoryMessagesResponse oResponse = new GetChatHistoryMessagesResponse()
			{
				Success = false,
				Messages = new List<TMessage>(),
			};

			try
			{
				var getMessages =
					await TelegramClientGet().MessagesService.GetHistoryAsync(targetUserPeer, offset, -1, messageLimit);

				oResponse.Messages =
					getMessages
						.As<TMessagesSlice>()
						.Messages
						.OfType<TMessage>()
						.ToList();

				oResponse.Success = true;
			}
			catch (Exception)
			{
				return (oResponse);
			}

			return (oResponse);
		}

		public static async Task<bool> Operation()
		{
			await TelegramClientGet().UsersService.GetCurrentUserFullAsync();

			Console.WriteLine("\nEnter Target Account Username Which You Want To Calculate The Chat Time: ");
			string strTargetUsername = System.Console.ReadLine();
			string strSearchValue =
					strTargetUsername.Trim().ToLower();
			if (strSearchValue.StartsWith("@"))
			{
				strSearchValue = strSearchValue.Substring(1);
			}
			string strSaveFile = string.Format("MessagesInChatWith-{0}.json", strTargetUsername);
			var chatsSearch = await TelegramClientGet().ContactsService.SearchUserAsync(strSearchValue);

			TUser oTargetUser =
				chatsSearch.Users.Where(current => current.GetType() == typeof(TUser)
					&& (!string.IsNullOrEmpty(((TUser)current).Username) && ((TUser)current).Username.Trim().ToLower() == strSearchValue))
				.FirstOrDefault()
				.As<TUser>();

			if (oTargetUser == null)
			{
				Console.WriteLine("Fail! Cannot Find Target User ...");
				return false;
			}

			List<TMessage> ChatMessages = new List<TMessage>();
			List<TmessageJustIntTime> ChatMessageTimes = new List<TmessageJustIntTime>();


			if (File.Exists(strSaveFile))
			{
				string strJsonDataOfSavedFile = string.Empty;
				using (StreamReader reader = new StreamReader(strSaveFile))
				{
					strJsonDataOfSavedFile = await reader.ReadToEndAsync();
				}

				ChatMessageTimes =
					Newtonsoft.Json.JsonConvert.DeserializeObject<List<TmessageJustIntTime>>(strJsonDataOfSavedFile);
			}
			else
			{
				var TargetUserPeer = new TInputPeerUser()
				{
					AccessHash = oTargetUser.AccessHash,
					UserId = oTargetUser.Id,
				};

				var ChatHisrtoy =
					await TelegramClientGet().MessagesService.GetHistoryAsync(TargetUserPeer, 0, -1, -1);
				int MessageCount = ChatHisrtoy.As<TMessagesSlice>().Count;
				Console.WriteLine(string.Format("\nYou Two Have {0} Messages In Your Telegram Chat", MessageCount));
				Console.WriteLine("Please Wait Until All Messages Load From Telegram! We Cant Download Them In One Package Because Of Telegram Flood Detection System!\n");

				int GetMessageLimit = 100;
				for (int i = 0; i < MessageCount; i += GetMessageLimit)
				{

					Console.WriteLine("Getting Messages Batch Number : " + i);

					GetChatHistoryMessagesResponse oResponse =
						await GetChatHistoryMessages(TargetUserPeer, GetMessageLimit, i);
					while (!oResponse.Success)
					{
						await Task.Delay(1000);

						oResponse =
							await GetChatHistoryMessages(TargetUserPeer, GetMessageLimit, i);
					}
					ChatMessages.AddRange(
						oResponse.Messages
						);

					await Task.Delay(1000);
				}
				ChatMessages = ChatMessages.DistinctBy(current => current.Id).ToList();

				ChatMessageTimes =
					ChatMessages
					.Select(currnet => new TmessageJustIntTime() { date = currnet.Date })
					.ToList();

				string strMessagesInJsonFormat =
					Newtonsoft.Json.JsonConvert.SerializeObject(ChatMessageTimes);

				using (StreamWriter writer = new StreamWriter(strSaveFile, false))
				{
					await writer.WriteAsync(strMessagesInJsonFormat);
				}
			}

			ChatMessageTimes = ChatMessageTimes
				.OrderBy(current=>current.date)
				.ToList();

			int SumOfChatsTimes = 0;
			List<List<TmessageJustIntTime>> Chats =
				new List<List<TmessageJustIntTime>>();



			int TempChatStartIndex = -1;
			int AllMessageCount = ChatMessageTimes.Count();
			for (int i = 0; i < AllMessageCount; i++)
			{
				if ((i + 1) == AllMessageCount)
				{
					continue;
				}

				if ((ChatMessageTimes[i + 1].date - ChatMessageTimes[i].date) > 150)
				{
					if (TempChatStartIndex == -1)
					{
						var chat = new List<TmessageJustIntTime>();
						chat.Add(ChatMessageTimes[i]);
						Chats.Add(chat);
					}
					else
					{
						var chat = new List<TmessageJustIntTime>();
						for (int j = TempChatStartIndex; j <= i; j++)
						{
							chat.Add(ChatMessageTimes[j]);
						}
						Chats.Add(chat);

						TempChatStartIndex = -1;
					}

				}
				else
				{
					if (TempChatStartIndex == -1)
					{
						TempChatStartIndex = i;
					}
				}
				
			}

			Chats.RemoveAll(current => current.Count() < 4);

			foreach (var chat in Chats)
			{
				int duration = chat[chat.Count() - 1].date - chat[0].date;
				SumOfChatsTimes += duration;
			}

			Console.WriteLine(string.Format("\n**************** Sum of All Your Chats Duration With {0} is {1} Seconds ****************", strTargetUsername, SumOfChatsTimes));

			System.TimeSpan chatsTimeSpan = System.TimeSpan.FromSeconds(SumOfChatsTimes);		
			Console.WriteLine(string.Format("\n**************** In Other Words {0} Days, {1} Hours, {2} Minutes and {3} Seconds ****************", chatsTimeSpan.Days, chatsTimeSpan.Hours, chatsTimeSpan.Minutes, chatsTimeSpan.Seconds));
			return true;
		}
	
	}
	public class GetChatHistoryMessagesResponse
	{
		public GetChatHistoryMessagesResponse()
		{

		}

		public bool Success { get; set; }
		public List<TMessage> Messages { get; set; }
	}

	public class TmessageJustIntTime
	{
		public TmessageJustIntTime()
		{

		}

		public int date { get; set; }
	}
}
