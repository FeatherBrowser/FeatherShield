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
        AddText("google-ima.js", "application/javascript", GoogleIma,
            "google-ima3", "imasdk.googleapis.com/js/sdkloader/ima3.js");
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
        if (colon > 0 && colon < value.Length - 1)
        {
            ReadOnlySpan<char> priority = value.AsSpan(colon + 1);
            if (priority[0] == '-')
                priority = priority[1..];
            if (priority.Length > 0 && priority.IndexOfAnyExceptInRange('0', '9') < 0)
                value = value[..colon];
        }

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

    private const string GoogleIma = """
        (function(){'use strict';
        if(window.google&&window.google.ima&&window.google.ima.AdsLoader)return;
        var noop=function(){};
        function Listeners(){this.l={};}
        Listeners.prototype.add=function(t,f,c){if(typeof f!=='function')return;(Array.isArray(t)?t:[t]).forEach(function(x){(this.l[x]=this.l[x]||[]).push({f:f,c:c});},this);};
        Listeners.prototype.remove=function(t,f){(Array.isArray(t)?t:[t]).forEach(function(x){var a=this.l[x];if(a)this.l[x]=a.filter(function(e){return e.f!==f;});},this);};
        Listeners.prototype.fire=function(t,ev){(this.l[t]||[]).slice().forEach(function(e){try{e.f.call(e.c||null,ev);}catch(err){setTimeout(function(){throw err;});}});};
        var ErrorCode={VAST_EMPTY_RESPONSE:1009,VAST_LOAD_TIMEOUT:301,VAST_NO_ADS_AFTER_WRAPPER:303,UNKNOWN_ERROR:900,FAILED_TO_REQUEST_ADS:1005,ADS_REQUEST_NETWORK_ERROR:1012,INVALID_ARGUMENTS:1101,AUTOPLAY_DISALLOWED:1205};
        function AdError(message,code,type){this.message=message;this.code=code;this.type=type||'adLoadError';}
        AdError.ErrorCode=ErrorCode;AdError.Type={AD_LOAD:'adLoadError',AD_PLAY:'adPlayError'};
        AdError.prototype.getErrorCode=function(){return this.code;};
        AdError.prototype.getVastErrorCode=function(){return 303;};
        AdError.prototype.getMessage=function(){return this.message;};
        AdError.prototype.getType=function(){return this.type;};
        AdError.prototype.getInnerError=function(){return null;};
        AdError.prototype.getAdId=function(){return '';};
        AdError.prototype.toString=function(){return 'AdError '+this.code+': '+this.message;};
        function AdErrorEvent(error,context){this.type='adError';this.error=error;this.context=context;}
        AdErrorEvent.Type={AD_ERROR:'adError'};
        AdErrorEvent.prototype.getError=function(){return this.error;};
        AdErrorEvent.prototype.getUserRequestContext=function(){return this.context===undefined?{}:this.context;};
        function AdsManagerLoadedEvent(){}
        AdsManagerLoadedEvent.Type={ADS_MANAGER_LOADED:'adsManagerLoaded'};
        AdsManagerLoadedEvent.prototype.getAdsManager=function(){return null;};
        AdsManagerLoadedEvent.prototype.getUserRequestContext=function(){return {};};
        var adEventTypes={};
        ['AD_BREAK_READY','AD_BUFFERING','AD_CAN_PLAY','AD_METADATA','AD_PROGRESS','ALL_ADS_COMPLETED','CLICK','COMPLETE','CONTENT_PAUSE_REQUESTED','CONTENT_RESUME_REQUESTED','DURATION_CHANGE','FIRST_QUARTILE','IMPRESSION','INTERACTION','LINEAR_CHANGED','LOADED','LOG','MIDPOINT','PAUSED','RESUMED','SKIPPABLE_STATE_CHANGED','SKIPPED','STARTED','THIRD_QUARTILE','USER_CLOSE','VIDEO_CLICKED','VIDEO_ICON_CLICKED','VOLUME_CHANGED','VOLUME_MUTED'].forEach(function(n){adEventTypes[n]=n.toLowerCase().replace(/_([a-z])/g,function(m,c){return c.toUpperCase();});});
        function AdEvent(type){this.type=type;}
        AdEvent.Type=adEventTypes;
        AdEvent.prototype.getAd=function(){return null;};
        AdEvent.prototype.getAdData=function(){return {};};
        function ImaSdkSettings(){this.locale='en';this.redirects=8;this.vpaid=0;this.autoPlay=true;this.cookies=true;this.playerType='';this.playerVersion='';this.ppid='';this.sessionId='';}
        ImaSdkSettings.VpaidMode={DISABLED:0,ENABLED:1,INSECURE:2};
        ImaSdkSettings.prototype={constructor:ImaSdkSettings,
          getLocale:function(){return this.locale;},setLocale:function(v){this.locale=v;},
          getNumRedirects:function(){return this.redirects;},setNumRedirects:function(v){this.redirects=v;},
          getVpaidMode:function(){return this.vpaid;},setVpaidMode:function(v){this.vpaid=v;},getVpaidAllowed:function(){return this.vpaid!==0;},setVpaidAllowed:function(v){this.vpaid=v?1:0;},
          getAutoPlayAdBreaks:function(){return this.autoPlay;},setAutoPlayAdBreaks:function(v){this.autoPlay=v;},
          getCookiesEnabled:function(){return this.cookies;},setCookiesEnabled:function(v){this.cookies=v;},
          getPlayerType:function(){return this.playerType;},setPlayerType:function(v){this.playerType=v;},
          getPlayerVersion:function(){return this.playerVersion;},setPlayerVersion:function(v){this.playerVersion=v;},
          getPpid:function(){return this.ppid;},setPpid:function(v){this.ppid=v;},
          getSessionId:function(){return this.sessionId;},setSessionId:function(v){this.sessionId=v;},
          getDisableCustomPlaybackForIOS10Plus:function(){return false;},setDisableCustomPlaybackForIOS10Plus:noop,
          getFeatureFlags:function(){return {};},setFeatureFlags:noop,setFeatureFlag:noop,
          setPageCorrelator:noop,setStreamCorrelator:noop,setCompanionBackfill:noop,setDisableFlashAds:noop,setAutoPlayAdBreaksSetting:noop};
        var settings=new ImaSdkSettings();
        function AdDisplayContainer(container,video){this.container=container;this.video=video;}
        AdDisplayContainer.prototype.initialize=noop;AdDisplayContainer.prototype.destroy=noop;
        function AdsLoader(){this.listeners=new Listeners();this.destroyed=false;}
        AdsLoader.prototype.addEventListener=function(t,f,u,c){this.listeners.add(t,f,c);};
        AdsLoader.prototype.removeEventListener=function(t,f){this.listeners.remove(t,f);};
        AdsLoader.prototype.getSettings=function(){return settings;};
        AdsLoader.prototype.getVersion=function(){return ima.VERSION;};
        AdsLoader.prototype.contentComplete=noop;
        AdsLoader.prototype.destroy=function(){this.destroyed=true;this.listeners=new Listeners();};
        AdsLoader.prototype.requestAds=function(request,context){
          var self=this;
          setTimeout(function(){
            if(self.destroyed)return;
            var error=new AdError('No ads were returned for this request.',ErrorCode.VAST_EMPTY_RESPONSE,'adLoadError');
            self.listeners.fire('adError',new AdErrorEvent(error,context));
          },0);
        };
        function AdsRequest(){this.adTagUrl='';this.adsResponse=null;this.linearAdSlotWidth=0;this.linearAdSlotHeight=0;this.nonLinearAdSlotWidth=0;this.nonLinearAdSlotHeight=0;this.forceNonLinearFullSlot=false;this.vastLoadTimeout=5000;}
        AdsRequest.prototype.setAdWillAutoPlay=noop;AdsRequest.prototype.setAdWillPlayMuted=noop;AdsRequest.prototype.setContinuousPlayback=noop;
        function AdsRenderingSettings(){this.restoreCustomPlaybackStateOnAdBreakComplete=false;this.enablePreloading=false;this.uiElements=null;this.loadVideoTimeout=8000;this.mimeTypes=null;this.bitrate=-1;}
        function CompanionAdSelectionSettings(){}
        CompanionAdSelectionSettings.CreativeType={ALL:'All',FLASH:'Flash',IMAGE:'Image'};
        CompanionAdSelectionSettings.ResourceType={ALL:'All',HTML:'Html',IFRAME:'IFrame',STATIC:'Static'};
        CompanionAdSelectionSettings.SizeCriteria={IGNORE:'IgnoreSize',SELECT_EXACT_MATCH:'SelectExactMatch',SELECT_NEAR_MATCH:'SelectNearMatch'};
        var ima={
          VERSION:'3.0.0',settings:settings,
          AdDisplayContainer:AdDisplayContainer,AdsLoader:AdsLoader,AdsRequest:AdsRequest,AdsRenderingSettings:AdsRenderingSettings,
          AdError:AdError,AdErrorEvent:AdErrorEvent,AdEvent:AdEvent,AdsManagerLoadedEvent:AdsManagerLoadedEvent,
          ImaSdkSettings:ImaSdkSettings,CompanionAdSelectionSettings:CompanionAdSelectionSettings,
          ViewMode:{NORMAL:'normal',FULLSCREEN:'fullscreen'},
          UiElements:{AD_ATTRIBUTION:'adAttribution',COUNTDOWN:'countdown'},
          OmidAccessMode:{LIMITED:'limited',DOMAIN:'domain',FULL:'full'},
          OmidVerificationVendor:{},
          UniversalAdIdInfo:function(){},
          AdCuePoints:function(){},
          AdPodInfo:function(){},
          CompanionAd:function(){}
        };
        window.google=window.google||{};
        window.google.ima=ima;
        })();
        """;
}
