The good news is, you don't have to know either of these two libraries above, using ModernHttpClient is the most boring thing in the world. Here's how it works:

On iOS:

```csharp
var httpClient = new HttpClient(new AFNetworkHandler());
```

On Android:

```csharp
var httpClient = new HttpClient(new OkHttpNetworkHandler());
```

## Other Resources

* [GitHub page](https://github.com/paulcbetts/ModernHttpClient)
* [AFNetworking 1.3.3 documentation](http://cocoadocs.org/docsets/AFNetworking/1.3.3/)
* [OkHttp site](http://square.github.io/okhttp/)
