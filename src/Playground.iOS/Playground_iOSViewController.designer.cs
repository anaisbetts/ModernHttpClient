// WARNING
//
// This file has been generated automatically by Xamarin Studio to store outlets and
// actions made in the UI designer. If it is removed, they will be lost.
// Manual changes to this file may not be handled correctly.
//
using MonoTouch.Foundation;
using System.CodeDom.Compiler;

namespace Playground.iOS
{
	[Register ("Playground_iOSViewController")]
	partial class Playground_iOSViewController
	{
		[Outlet]
		MonoTouch.UIKit.UITextView result { get; set; }

        [Action ("cancelIt:")]
		partial void cancel (MonoTouch.Foundation.NSObject sender);

		[Action ("doIt:")]
		partial void doIt (MonoTouch.Foundation.NSObject sender);
		
		void ReleaseDesignerOutlets ()
		{
			if (result != null) {
				result.Dispose ();
				result = null;
			}
		}
	}
}
