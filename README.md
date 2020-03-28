# NSocks
.NET HttpClient proxy handler implementation for SOCKS proxies.

Contains a reimplementation of the HTTP stack if you need to do direct HTTP manipulation with raw sockets/streams.

### Protocol support

#### SOCKS version
| Socks version  | Supported |
|----------------|-----------|
| Socks4         | ❌         |
| Socks4a        | ❌         |
| Socks5         | ✔️         |
| Socks6 (draft) | ❌         |

#### HTTP version
| HTTP version | Supported |
|--------------|-----------|
| HTTP/0.9     | ❌         |
| HTTP/1.0     | ❌         |
| HTTP/1.1     | ✔️         |
| HTTP/2.0     | ❌         |

### Usage

Here's an example usage of using a proxy handler in your HttpClient:

```cs
var proxyUri = new Uri("socks5://127.0.0.1:1080/");

var handler = new Socks5Handler(proxyUri, "username", "password");
using var httpClient = new HttpClient(handler);

await httpClient.GetAsync("https://www.ietf.org/rfc/rfc1928.txt");
```
