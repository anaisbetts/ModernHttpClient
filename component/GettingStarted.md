The good news is, you don't have to know either of these two libraries above,
using ModernHttpClient is the most boring thing in the world. Here's how it
works:

```csharp
var httpClient = new HttpClient(new NativeMessageHandler());
```

## Other Resources

* [GitHub page](https://github.com/paulcbetts/ModernHttpClient)
* [OkHttp site](http://square.github.io/okhttp/)
