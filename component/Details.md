This library brings the latest platform-specific networking libraries to Xamarin applications via a custom HttpClient handler. Write your app using System.Net.Http, but drop this library in and it will go drastically faster.  This is made possible by two native libraries:

* On iOS, via NSURLSession
* On Android, via [OkHttp 1.5](http://square.github.io/okhttp/)

## Usage

The good news is, you don't have to know either of these two libraries above,
using ModernHttpClient is the most boring thing in the world. Here's how it
works:

```csharp
var httpClient = new HttpClient(new NativeMessageHandler());
```
