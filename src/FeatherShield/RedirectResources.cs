using System.Text;

namespace FeatherShield;

public sealed record RedirectResource(string Name, string MimeType, ReadOnlyMemory<byte> Body);

public static class RedirectResources
{
    public const string None = "none";

    private static readonly Dictionary<string, RedirectResource> Resources = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, string> Aliases = new(StringComparer.OrdinalIgnoreCase);

    static RedirectResources()
    {
        AddText("noop.js", "application/javascript", "(function(){})();", "noopjs", "blank-js");
        AddText("noop.txt", "text/plain", "", "nooptext", "blank-text", "empty");
        AddText("noop.html", "text/html",
            "<!DOCTYPE html><html><head></head><body></body></html>", "noopframe", "blank-html");
        AddText("noop.css", "text/css", "", "noopcss", "blank-css");
        AddText("noop.json", "application/json", "{}", "noopjson");

        AddBase64("1x1.gif", "image/gif",
            "R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7",
            "1x1-transparent.gif", "1x1-transparent-gif", "blank-gif");
        AddBase64("2x2.png", "image/png",
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNkYAAAAAYAAjCB0C8AAAAASUVORK5CYII=",
            "2x2-transparent.png", "1x1.png", "1x1-transparent.png", "3x2.png", "32x32.png", "blank-png");

        // Empty media bodies: players report "no ad" instead of retrying or flagging a blocker.
        AddText("noop-0.1s.mp3", "audio/mpeg", "", "noopmp3-0.1s", "blank-mp3");
        AddText("noop-1s.mp4", "video/mp4", "", "noopmp4-1s", "blank-mp4", "mp4");

        AddText("noop-vast2.xml", "application/xml",
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?><VAST version=\"2.0\"></VAST>", "noopvast-2.0");
        AddText("noop-vast3.xml", "application/xml",
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?><VAST version=\"3.0\"></VAST>", "noopvast-3.0");
        AddText("noop-vast4.xml", "application/xml",
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?><VAST version=\"4.0\"></VAST>", "noopvast-4.0");
        AddText("noop-vmap1.xml", "application/xml",
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?><vmap:VMAP xmlns:vmap=\"http://www.iab.net/videosuite/vmap\" version=\"1.0\"></vmap:VMAP>",
            "noopvmap-1.0");

        AddText("googlesyndication_adsbygoogle.js", "application/javascript", AdsByGoogle,
            "googlesyndication.com/adsbygoogle.js", "googlesyndication-adsbygoogle");
        AddText("googletagservices_gpt.js", "application/javascript", GooglePublisherTag,
            "googletagservices.com/gpt.js", "googletagservices-gpt");
        AddText("google-analytics_analytics.js", "application/javascript", GoogleAnalytics,
            "google-analytics.com/analytics.js", "google-analytics-analytics");
        AddText("googletagmanager_gtm.js", "application/javascript", GoogleTagManager,
            "googletagmanager.com/gtm.js", "googletagmanager-gtm");
        AddText("fuckadblock.js-3.2.0", "application/javascript", AdBlockDetectorLibrary,
            "fuckadblock.js", "nofab.js", "blockadblock.js");
        AddText("amazon_apstag.js", "application/javascript", AmazonPublisherTag,
            "amazon-apstag");
        AddText("scorecardresearch_beacon.js", "application/javascript", ComscoreBeacon,
            "scorecardresearch.com/beacon.js", "scorecardresearch-beacon");
    }

    public static IReadOnlyCollection<string> Names => Resources.Keys;

    public static string? Normalize(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        string value = name.Trim();

        if (value.StartsWith("abp-resource:", StringComparison.OrdinalIgnoreCase))
            value = value["abp-resource:".Length..];

        int colon = value.LastIndexOf(':');
        if (colon > 0 && colon < value.Length - 1 && value.AsSpan(colon + 1).IndexOfAnyExceptInRange('0', '9') < 0)
            value = value[..colon];

        if (value.Equals(None, StringComparison.OrdinalIgnoreCase))
            return None;

        if (Resources.ContainsKey(value))
            return Resources[value].Name;

        return Aliases.TryGetValue(value, out string? canonical) ? canonical : null;
    }

    public static bool TryGet(string? name, out RedirectResource resource)
    {
        string? canonical = Normalize(name);
        if (canonical is not null && Resources.TryGetValue(canonical, out RedirectResource? found))
        {
            resource = found;
            return true;
        }

        resource = null!;
        return false;
    }

    private static void AddText(string name, string mime, string body, params string[] aliases) =>
        Add(name, mime, Encoding.UTF8.GetBytes(body), aliases);

    private static void AddBase64(string name, string mime, string body, params string[] aliases) =>
        Add(name, mime, Convert.FromBase64String(body), aliases);

    private static void Add(string name, string mime, byte[] body, string[] aliases)
    {
        Resources[name] = new RedirectResource(name, mime, body);
        foreach (string alias in aliases)
            Aliases[alias] = name;
    }

    private const string AdsByGoogle = """
        (function(){'use strict';
        var queued=window.adsbygoogle;
        function fill(){var slots=document.querySelectorAll('ins.adsbygoogle');for(var i=0;i<slots.length;i++){var s=slots[i];if(s.getAttribute('data-adsbygoogle-status'))continue;s.setAttribute('data-adsbygoogle-status','done');s.setAttribute('data-ad-status','unfilled');}}
        var api={loaded:true,push:function(){fill();return 0;}};
        window.adsbygoogle=api;
        if(Array.isArray(queued)){for(var i=0;i<queued.length;i++)api.push(queued[i]);}
        if(document.readyState==='loading')document.addEventListener('DOMContentLoaded',fill,{once:true});else fill();
        })();
        """;

    private const string GooglePublisherTag = """
        (function(){'use strict';
        var noop=function(){};
        function chain(o,names){names.forEach(function(n){o[n]=function(){return o;};});return o;}
        function list(){return [];}
        function slot(path,div){
          var s=chain({},['addService','clearCategoryExclusions','clearTargeting','defineSizeMapping','set','setCategoryExclusion','setClickUrl','setCollapseEmptyDiv','setConfig','setForceSafeFrame','setSafeFrameConfig','setTargeting','updateTargetingFromMap']);
          s.getAdUnitPath=function(){return path||'';};
          s.getSlotElementId=function(){return div||'';};
          s.getDomId=s.getSlotElementId;
          s.getSlotId=function(){return {getDomId:s.getSlotElementId,getId:s.getAdUnitPath};};
          s.getTargeting=list;s.getTargetingKeys=list;s.getSizes=list;s.getAttributeKeys=list;s.getCategoryExclusions=list;
          s.getResponseInformation=function(){return null;};s.get=function(){return null;};
          return s;
        }
        var pubads=chain({},['addEventListener','clear','clearCategoryExclusions','clearTagForChildDirectedTreatment','clearTargeting','collapseEmptyDivs','disableInitialLoad','display','enableAsyncRendering','enableLazyLoad','enableSingleRequest','enableSyncRendering','enableVideoAds','refresh','removeEventListener','set','setCategoryExclusion','setCentering','setCookieOptions','setForceSafeFrame','setLocation','setPrivacySettings','setPublisherProvidedId','setRequestNonPersonalizedAds','setSafeFrameConfig','setTagForChildDirectedTreatment','setTargeting','setVideoContent','updateCorrelator']);
        pubads.getSlots=list;pubads.getTargeting=list;pubads.getTargetingKeys=list;pubads.getAttributeKeys=list;
        pubads.get=function(){return null;};pubads.isInitialLoadDisabled=function(){return false;};
        pubads.definePassback=function(p){return slot(p,'');};pubads.defineOutOfPagePassback=pubads.definePassback;
        var companion=chain({},['addEventListener','enableSyncLoading','removeEventListener','set','setRefreshUnfilledSlots','setVideoContent']);
        var content=chain({},['addEventListener','removeEventListener','set','setContent']);
        var gt=window.googletag||{};var queued=gt.cmd;
        gt.apiReady=true;gt.pubadsReady=true;
        gt.pubads=function(){return pubads;};gt.companionAds=function(){return companion;};gt.content=function(){return content;};
        gt.defineSlot=function(p,size,d){return slot(p,d);};gt.defineOutOfPageSlot=function(p,d){return slot(p,typeof d==='string'?d:'');};
        gt.destroySlots=function(){return true;};gt.getVersion=function(){return '';};
        gt.disablePublisherConsole=noop;gt.display=noop;gt.enableServices=noop;gt.onPubAdsApiAvailable=noop;gt.openConsole=noop;gt.setAdIframeTitle=noop;gt.setConfig=noop;
        gt.sizeMapping=function(){var b={addSize:function(){return b;},build:list};return b;};
        gt.secureSignalProviders={push:noop};
        gt.cmd={push:function(){for(var i=0;i<arguments.length;i++){try{arguments[i].call(window);}catch(e){}}return 1;}};
        window.googletag=gt;
        if(Array.isArray(queued))gt.cmd.push.apply(gt.cmd,queued);
        })();
        """;

    private const string GoogleAnalytics = """
        (function(){'use strict';
        var noop=function(){};
        function tracker(){return {get:function(){return undefined;},set:noop,send:noop};}
        function ga(){
          var a=arguments,last=a[a.length-1],cb=null;
          if(last&&typeof last==='object'&&typeof last.hitCallback==='function')cb=last.hitCallback;
          else if(typeof a[0]==='function'){var f=a[0];cb=function(){f(tracker());};}
          if(cb){try{cb();}catch(e){}}
        }
        ga.create=tracker;ga.getByName=tracker;ga.getAll=function(){return [];};ga.remove=noop;ga.loaded=true;
        var name=window.GoogleAnalyticsObject||'ga';
        var queued=window[name]&&window[name].q;
        window[name]=ga;
        if(Array.isArray(queued)){for(var i=0;i<queued.length;i++){try{ga.apply(null,queued[i]);}catch(e){}}}
        })();
        """;

    private const string GoogleTagManager = """
        (function(){'use strict';
        function run(o){if(o&&typeof o==='object'&&typeof o.eventCallback==='function'){var f=o.eventCallback;setTimeout(function(){try{f();}catch(e){}},1);}}
        var dl=window.dataLayer;
        if(Array.isArray(dl)){var push=dl.push;dl.push=function(){for(var i=0;i<arguments.length;i++)run(arguments[i]);return push.apply(dl,arguments);};dl.forEach(run);}
        window.google_tag_manager=window.google_tag_manager||{};
        })();
        """;

    private const string AdBlockDetectorLibrary = """
        (function(){'use strict';
        function Detector(){this._clean=[];}
        var p=Detector.prototype;
        p.setOption=function(){return this;};
        p.emitEvent=function(){return this;};
        p.clearEvent=function(){this._clean=[];return this;};
        p.check=function(){this._clean.forEach(function(f){try{f();}catch(e){}});return true;};
        p.on=function(detected,fn){if(!detected&&typeof fn==='function'){this._clean.push(fn);setTimeout(function(){try{fn();}catch(e){}},1);}return this;};
        p.onDetected=function(){return this;};
        p.onNotDetected=function(fn){return this.on(false,fn);};
        ['FuckAdBlock','BlockAdBlock','SniffAdBlock'].forEach(function(n){window[n]=Detector;});
        ['fuckAdBlock','blockAdBlock','sniffAdBlock'].forEach(function(n){window[n]=new Detector();});
        })();
        """;

    private const string AmazonPublisherTag = """
        (function(){'use strict';
        var noop=function(){};
        window.apstag={_Q:[],init:noop,setDisplayBids:noop,renderImp:noop,deleteId:noop,rpa:noop,upa:noop,dpa:noop,thirdPartyData:{},
          targetingKeys:function(){return [];},
          fetchBids:function(cfg,cb){if(typeof cb==='function')setTimeout(function(){try{cb([]);}catch(e){}},1);}};
        })();
        """;

    private const string ComscoreBeacon = """
        (function(){'use strict';
        var noop=function(){};
        window.COMSCORE={beacon:noop,purge:function(){window._comscore=[];}};
        })();
        """;
}
