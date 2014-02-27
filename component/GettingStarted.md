The good news is, you don't have to know either of these two libraries above,
using ModernHttpClient is the most boring thing in the world. Here's how it
works:

On iOS:

```csharp
var httpClient = new HttpClient(new NSUrlSessionHandler());
```

On Android:

```csharp
var httpClient = new HttpClient(new OkHttpNetworkHandler());
```

## Other Resources

* [GitHub page](https://github.com/paulcbetts/ModernHttpClient)
* [OkHttp site](http://square.github.io/okhttp/)
