using System.Runtime.InteropServices;
using System.Net.Sockets;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using TEKSteamClient;
using TEKSteamClient.CM;
using MRCP;

AppDomain.CurrentDomain.UnhandledException += (sender, e) => Console.WriteLine($"Unhandled exception caught: {e.ExceptionObject}");
Environment.ExitCode = -1;
if (args.Length > 0 && args[0] is "--setup")
{
	Console.Write("Enter the port to listen on: ");
	if (!ushort.TryParse(Console.ReadLine(), out ushort port))
	{
		Console.WriteLine("Error: Invalid port value");
		return;
	}
	Console.Write("Enter Steam account login: ");
	string accountName = Console.ReadLine()!;
	Console.Write("Enter Steam account password: ");
	var builder = new StringBuilder();
	ConsoleKey key;
	do
	{
		var keyInfo = Console.ReadKey(true);
		key = keyInfo.Key;
		if (key is ConsoleKey.Backspace && builder.Length > 0)
			builder.Remove(builder.Length - 1, 1);
		else if (!char.IsControl(keyInfo.KeyChar))
			builder.Append(keyInfo.KeyChar);
	} while (key != ConsoleKey.Enter);
	Console.WriteLine();
	var cmClient = new CMClient();
	string? token = null;
	try { cmClient.LogOn(accountName, ref token, builder.ToString()); }
	catch (SteamException se)
	{
		Console.WriteLine($"Log on failed: {se.Message}");
		return;
	}
	cmClient.Disconnect();
	new Config
	{
		Port = port,
		AccountName = accountName,
		Token = token!
	}.SaveToFile();
	Environment.ExitCode = 0;
}
else
{
	var config = Config.Load();
	var cmClient = new CMClient();
	string? token = config.Token;
	cmClient.LogOn(config.AccountName, ref token);
	using var cts = new CancellationTokenSource();
	using var signalRegistration = PosixSignalRegistration.Create(PosixSignal.SIGTERM, delegate
	{
		Environment.ExitCode = 0;
		cts.Cancel();
	});
	using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp) { SendTimeout = 2000 };
	socket.Bind(new IPEndPoint(IPAddress.Any, config.Port));
	byte[] buffer = new byte[16];
	ref byte bufferRef = ref MemoryMarshal.GetArrayDataReference(buffer);
	var sendSpan = new ReadOnlySpan<byte>(buffer, 0, 8);
	var remoteAddress = new SocketAddress(AddressFamily.InterNetwork);
	try
	{
		while (!cts.IsCancellationRequested)
		{
			var task = socket.ReceiveFromAsync(buffer, SocketFlags.None, remoteAddress, cts.Token);
			if ((task.IsCompletedSuccessfully ? task.GetAwaiter().GetResult() : task.AsTask().GetAwaiter().GetResult()) < 16)
				continue;
			ulong requestCode;
		tryAgain:
			try
			{
				requestCode = cmClient.GetManifestRequestCode(Unsafe.As<byte, uint>(ref bufferRef), Unsafe.As<byte, uint>(ref Unsafe.AddByteOffset(ref bufferRef, 4)), Unsafe.As<byte, ulong>(ref Unsafe.AddByteOffset(ref bufferRef, 8)));
			}
			catch (SteamException se) when (se.Type is SteamException.ErrorType.CMNotLoggedOn)
			{
				try
				{
					cmClient.LogOn(config.AccountName, ref token);
					goto tryAgain;
				}
				catch (Exception e)
				{
					Console.WriteLine(e);
					continue;
				}
			}
			catch (Exception e)
			{
				Console.WriteLine(e);
				continue;
			}
			Unsafe.As<byte, ulong>(ref bufferRef) = requestCode;
			socket.SendTo(sendSpan, SocketFlags.None, remoteAddress);
		}
	}
	catch (OperationCanceledException) { }
}