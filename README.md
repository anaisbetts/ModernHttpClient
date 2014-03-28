ModernHttpClient
================

This library brings the latest platform-specific networking libraries to
Xamarin applications via a custom HttpClient handler. Write your app using
System.Net.Http, but drop this library in and it will go drastically faster.
This is made possible by two native libraries:

* On iOS, [NSURLSession](https://developer.apple.com/library/ios/documentation/Foundation/Reference/NSURLSession_class/Introduction/Introduction.html)
* On Android, via [OkHttp 1.5](http://square.github.io/okhttp/)

## Usage

The good news is, you don't have to know either of these two libraries above,
using ModernHttpClient is the most boring thing in the world. Here's how
it works:

On iOS:

```cs
var httpClient = new HttpClient(new NSUrlSessionHandler());
```

On Android:

```cs
var httpClient = new HttpClient(new OkHttpNetworkHandler());
```

## How can I use this in a PCL?

Using ModernHttpClient from a PCL is fairly easy with some rigging, especially
if you've got some sort of IoC/DI setup - request an HttpClient in your PCL,
and register it in your app. However, here's what you can do without any
external dependencies:

```cs
// In your PCL
public static class HttpClientFactory 
{
    public static Func<HttpClient> Get { get; set; }
    
    static HttpClientFactory()
    {
        Get = (() => new HttpClient());
    }
}

// Somewhere else in your PCL
var client = HttpClientFactory.Get();

// In your iOS app (i.e. the startup of your app)
public static class AppDelegate
{
    public void FinishedLaunching(UIApplication app, NSDictionary options)
    {
        HttpClientFactory.Get = (() => new HttpClient(new NSUrlSessionHandler()));
    }
}
```

## How can I use this in MvvmCross?

Check out Michael Ridland's blog post, [Implementing ModernHttpClient in
MvvmCross](http://www.michaelridland.com/mobile/implementing-modernhttpclient-in-mvvmcross/),
for more information.

## Building

```sh
make
```
