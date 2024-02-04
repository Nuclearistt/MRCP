# MRCP
[![Discord](https://img.shields.io/discord/937821572285206659?style=flat-square&label=Discord&logo=discord&logoColor=white&color=7289DA)](https://discord.com/servers/teknology-hub-937821572285206659)

## Overview

Steam Manifest Request Code Provider (MRCP) is a lightweight bot that supplies Steam manifest request codes over a UDP socket using your Steam account

## How to use

Download the binary for your OS in [releases](https://github.com/Nuclearistt/MRCP/releases) or build it manually (relevant for Linux since Native AOT adds dynamic dependencies on specific library versions), run it once with `--setup` argument to interactively input credentials, then you may run it without any arguments (e.g as as service), it'll listen on specified port for UDP requests

## Request and response data formats

Request (16 bytes):
+ uint AppId
+ uint DepotId
+ ulong ManifestId

Response (8 bytes): ulong ManifestRequestCode

## Why do you need it

While [TEK Steam Client](https://github.com/Nuclearistt/TEKSteamClient) can install arbitrary Steam apps, it is unable to get manifest request codes for apps not owned on the account it's logged on, and hence to download those apps. MCRP allows to proxy MCR requests to an account that owns the apps in question without leaking account credentials because it's running on a remote server

## Client side code example

```cs
using System.Collections.Frozen;
using System.Net.Sockets;
using TEKSteamClient;

static readonly IPEndPoint ServerEndpoint = IPEndPoint.Parse("*Your server IP*:*MRCP Port*");
static ulong GetManifestRequestCode(uint appId, uint depotId, ulong manifestId)
{
	using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp); //May make it a static singleton
	Span<byte> buffer = stackalloc byte[16];
	BitConverter.TryWriteBytes(buffer[..4], appId);
	BitConverter.TryWriteBytes(buffer[4..8], depotId);
	BitConverter.TryWriteBytes(buffer[8..], manifestId);
	socket.SendTo(buffer, ServerEndpoint);
	socket.Receive(buffer);
	return BitConverter.ToUInt64(buffer);
}

//The following code needs to be executed only once per app lifetime, you may put it in the beginning of Main method
CMClient.ManifestRequestCodeSourceOverrides = System.Collections.Frozen.FrozenDictionary.ToFrozenDictionary((IEnumerable<KeyValuePair<uint, Func<uint, uint, ulong, ulong>>>)
	[
		new(/*A depot ID*/, GetManifestRequestCode),
		new(/*Another depot ID*/, GetManifestRequestCode)
	]);

//Your code using TEK Steam Client goes here, it'll automatically send requests to MRCP when needed
```

## License

MRCP is licensed under the [MIT](https://github.com/Nuclearistt/MRCP/blob/main/LICENSE) license.