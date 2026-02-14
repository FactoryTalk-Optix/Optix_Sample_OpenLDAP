#region Using directives
using System;
using UAManagedCore;
using OpcUa = UAManagedCore.OpcUa;
using FTOptix.NativeUI;
using FTOptix.HMIProject;
using FTOptix.Retentivity;
using FTOptix.UI;
using FTOptix.NetLogic;
using FTOptix.CoreBase;
using FTOptix.Core;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.Text;
#endregion

public class LoginWebBrowserLogic : BaseNetLogic
{
    public override void Start()
    {
        try
        {
            var webBrowser = (WebBrowser)Owner;

            webBrowser.OnURLRedirection += WebBrowser_OnURLRedirection;

            Log.Info("Starting OAuth2 authentication flow");
            Session.StartOAuth2Flow(webBrowser.NodeId, OAuth2Completed);
        }
        catch (Exception e)
        {
            Log.Error("LoginWebBrowserLogic", e.Message);
        }
    }

    private void WebBrowser_OnURLRedirection(object sender, URLRedirectionEvent e)
    {
        try
        {
            Log.Info($"Redirect event received with URL {e.URL}");
            var webBrowser = (WebBrowser)sender;
            webBrowser.Visible = false;
        }
        catch (Exception ex)
        {
            Log.Error("LoginWebBrowserLogic", ex.Message);
        }
    }
    private static string GetResultMessage(OAuth2ResultCode result)
    {
        string resultString = result switch
        {
            OAuth2ResultCode.Success => "Success",
            OAuth2ResultCode.ConfigurationError => "Configuration error",
            OAuth2ResultCode.InvalidState => "The state string received does not match the state string sent",
            OAuth2ResultCode.HttpClientError => "HTTP client error",
            OAuth2ResultCode.InvalidToken => "Invalid JWT token",
            OAuth2ResultCode.ChangeUserError => "Error while changing user",
            _ => "Unknown result"
        };

        return resultString;
    }

    private void OAuth2Completed(OAuth2ResultCode result)
    {
        if (result == OAuth2ResultCode.Success)
        {
            Log.Info($"OAuth2 flow completed successfully");
            return;
        }

        var webBrowser = (WebBrowser)Owner;
        var panel = (Panel)webBrowser.Owner;
        var statusLabel = panel.Get<Label>("StatusLabel");
        var message = GetResultMessage(result);
        webBrowser.Visible = false;
        statusLabel.Text = message;
        Log.Info($"OAuth2 Flow completed ({result}): {message}");
    }
}
