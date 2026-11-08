using System.Text;
using System.Text.Json;

namespace FeatherShield.Scriptlets;

internal static class ScriptletLibrary
{
    private static readonly Dictionary<string, string> Names = BuildNames();

    public static string? Canonicalize(string name)
    {
        string value = name.Trim();

        if (value.StartsWith("ubo-", StringComparison.OrdinalIgnoreCase))
            value = value[4..];

        if (value.EndsWith(".js", StringComparison.OrdinalIgnoreCase) &&
            !value.Equals("fuckadblock.js-3.2.0", StringComparison.OrdinalIgnoreCase))
        {
            value = value[..^3];
        }

        return Names.TryGetValue(value, out string? canonical) ? canonical : null;
    }

    public static IReadOnlyCollection<string> SupportedNames => Names.Values.Distinct().ToArray();

    public static string BuildScript(IReadOnlyList<ScriptletCall> calls)
    {
        var json = new StringBuilder("[");
        for (int i = 0; i < calls.Count; i++)
        {
            if (i > 0)
                json.Append(',');

            json.Append('[')
                .Append(JsonSerializer.Serialize(calls[i].Name))
                .Append(',')
                .Append(JsonSerializer.Serialize(calls[i].Args))
                .Append(']');
        }

        json.Append(']');

        return "(function(){'use strict';try{" + Library +
               "var calls=" + json + ";" +
               "for(var i=0;i<calls.length;i++){var f=lib[calls[i][0]];if(f){try{f.apply(null,calls[i][1]);}catch(e){}}}" +
               "}catch(e){}})();";
    }

    private static Dictionary<string, string> BuildNames()
    {
        var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        void Add(string canonical, params string[] aliases)
        {
            names[canonical] = canonical;
            foreach (string alias in aliases)
                names[alias] = canonical;
        }

        Add("set-constant", "set", "override-property-read");
        Add("abort-on-property-read", "aopr");
        Add("abort-on-property-write", "aopw");
        Add("abort-current-script", "acs");
        Add("abort-current-inline-script", "acis");
        Add("no-setTimeout-if", "nostif", "prevent-setTimeout", "setTimeout-defuser");
        Add("no-setInterval-if", "nosiif", "prevent-setInterval", "setInterval-defuser");
        Add("addEventListener-defuser", "aeld", "prevent-addEventListener", "prevent-listener");
        Add("no-fetch-if", "prevent-fetch");
        Add("no-xhr-if", "prevent-xhr");
        Add("no-window-open-if", "nowoif", "prevent-window-open", "window.open-defuser");
        Add("json-prune");
        Add("remove-attr", "ra");
        Add("remove-class", "rc");
        Add("set-local-storage-item");
        Add("set-session-storage-item");
        Add("nano-setTimeout-booster", "nano-stb", "adjust-setTimeout");
        Add("nano-setInterval-booster", "nano-sib", "adjust-setInterval");
        Add("noeval-if", "prevent-eval-if", "noeval", "silent-noeval");
        Add("nowebrtc", "prevent-webrtc");
        Add("set-cookie");
        Add("nofab", "prevent-fab-3.2.0", "fuckadblock.js-3.2.0");
        Add("nobab", "prevent-bab", "bab-defuser");
        return names;
    }

    private const string Library = """
        var W=window,lib={};
        var nativeTimeout=W.setTimeout.bind(W);
        var ownErrors=typeof WeakSet==='function'?new WeakSet():null,errorHookInstalled=false;
        function abortError(){
          var e=new ReferenceError('Script error.');
          if(ownErrors){ownErrors.add(e);
            if(!errorHookInstalled){errorHookInstalled=true;
              W.addEventListener('error',function(ev){if(ev.error&&ownErrors.has(ev.error)){ev.preventDefault();ev.stopImmediatePropagation();}},true);}}
          return e;}
        function matcher(raw){
          raw=raw==null?'':String(raw);var negate=false;
          if(raw.charAt(0)==='!'){negate=true;raw=raw.slice(1);}
          var test;
          if(raw===''){test=function(){return true;};}
          else{var end=raw.lastIndexOf('/');
            if(raw.length>2&&raw.charAt(0)==='/'&&end>0){
              try{var re=new RegExp(raw.slice(1,end),raw.slice(end+1));test=function(t){return re.test(t);};}
              catch(e){test=function(){return false;};}}
            else{test=function(t){return String(t).indexOf(raw)!==-1;};}}
          return function(t){var hit=test(String(t));return negate?!hit:hit;};}
        function fnText(f){
          try{if(typeof f==='function')return Function.prototype.toString.call(f);
            if(f&&typeof f.handleEvent==='function')return Function.prototype.toString.call(f.handleEvent);}catch(e){}
          return String(f);}
        function isObj(v){return v!==null&&(typeof v==='object'||typeof v==='function');}
        function wrap(owner,prop,decide){
          var orig;try{orig=owner[prop];}catch(e){return;}
          if(typeof orig!=='function')return;
          var proxy=new Proxy(orig,{apply:function(target,self,args){
            var out;try{out=decide(target,self,args);}catch(e){out=undefined;}
            if(out&&out.skip)return out.value;
            return Reflect.apply(target,self,out&&out.args?out.args:args);}});
          try{owner[prop]=proxy;}catch(e){}}
        function onChain(chain,leaf){
          function walk(owner,path){
            var dot=path.indexOf('.');
            if(dot<0){leaf(owner,path);return;}
            var prop=path.slice(0,dot),rest=path.slice(dot+1),v;
            try{v=owner[prop];}catch(e){return;}
            if(isObj(v)){walk(v,rest);return;}
            var d=Object.getOwnPropertyDescriptor(owner,prop);
            if(d&&!d.configurable)return;
            var stored=v;
            Object.defineProperty(owner,prop,{configurable:true,enumerable:true,
              get:function(){return stored;},
              set:function(nv){stored=nv;if(isObj(nv))walk(nv,rest);}});}
          if(chain)walk(W,String(chain));}
        function constant(raw){
          switch(raw){
            case 'undefined':return {v:undefined};case 'false':return {v:false};case 'true':return {v:true};
            case 'null':return {v:null};case '':case "''":case 'emptyStr':return {v:''};
            case 'noopFunc':return {v:function(){}};case 'trueFunc':return {v:function(){return true;}};
            case 'falseFunc':return {v:function(){return false;}};
            case 'throwFunc':return {v:function(){throw new Error();}};
            case 'noopCallbackFunc':return {v:function(){return function(){};}};
            case 'noopPromiseResolve':return {v:function(){return Promise.resolve();}};
            case 'emptyArr':case '[]':return {v:[]};case 'emptyObj':case '{}':return {v:{}};
            case '-1':return {v:-1};case 'yes':return {v:'yes'};case 'no':return {v:'no'};}
          if(/^\d+$/.test(raw)&&+raw<=32767)return {v:+raw};
          return null;}

        lib['set-constant']=function(chain,raw){
          var c=constant(raw==null?'undefined':String(raw));if(!c)return;
          onChain(chain,function(owner,prop){
            var d=Object.getOwnPropertyDescriptor(owner,prop);
            if(d&&!d.configurable){try{owner[prop]=c.v;}catch(e){}return;}
            Object.defineProperty(owner,prop,{configurable:true,enumerable:true,get:function(){return c.v;},set:function(){}});});};

        lib['abort-on-property-read']=function(chain){
          onChain(chain,function(owner,prop){
            var d=Object.getOwnPropertyDescriptor(owner,prop);if(d&&!d.configurable)return;
            Object.defineProperty(owner,prop,{configurable:true,get:function(){throw abortError();},set:function(){}});});};

        lib['abort-on-property-write']=function(chain){
          onChain(chain,function(owner,prop){
            var d=Object.getOwnPropertyDescriptor(owner,prop);if(d&&!d.configurable)return;
            var v;try{v=owner[prop];}catch(e){}
            Object.defineProperty(owner,prop,{configurable:true,get:function(){return v;},set:function(){throw abortError();}});});};

        function abortCurrent(chain,needle,inlineOnly){
          var match=matcher(needle);
          onChain(chain,function(owner,prop){
            var d=Object.getOwnPropertyDescriptor(owner,prop);if(d&&!d.configurable)return;
            var getter=d&&d.get,setter=d&&d.set,value=d&&('value' in d)?d.value:undefined;
            function check(){
              var s=document.currentScript;
              if(!s||!(s instanceof HTMLScriptElement))return;
              if(inlineOnly&&s.src)return;
              var text=s.src?s.src+'\n'+(s.textContent||''):(s.textContent||'');
              if(match(text))throw abortError();}
            Object.defineProperty(owner,prop,{configurable:true,enumerable:d?d.enumerable:true,
              get:function(){check();return getter?getter.call(this):value;},
              set:function(nv){check();if(setter)setter.call(this,nv);else value=nv;}});});}
        lib['abort-current-script']=function(chain,needle){abortCurrent(chain,needle,false);};
        lib['abort-current-inline-script']=function(chain,needle){abortCurrent(chain,needle,true);};

        function noTimerIf(name,needle,delayRaw){
          if(!needle&&!delayRaw)return;
          var match=matcher(needle),delay=null,delayNot=false;
          if(delayRaw){delayRaw=String(delayRaw);if(delayRaw.charAt(0)==='!'){delayNot=true;delayRaw=delayRaw.slice(1);}
            delay=parseInt(delayRaw,10);if(isNaN(delay))delay=null;}
          wrap(W,name,function(t,self,args){
            if(!match(fnText(args[0])))return;
            if(delay!==null&&((+args[1]===delay)===delayNot))return;
            var a=Array.prototype.slice.call(args);a[0]=function(){};return {args:a};});}
        lib['no-setTimeout-if']=function(n,d){noTimerIf('setTimeout',n,d);};
        lib['no-setInterval-if']=function(n,d){noTimerIf('setInterval',n,d);};

        function boostTimer(name,needle,delayRaw,boostRaw){
          var match=matcher(needle==='*'?'':needle);
          var delay=delayRaw==='*'?null:parseInt(delayRaw||'1000',10);
          var boost=parseFloat(boostRaw||'0.05');
          if(!isFinite(boost)||boost<=0)boost=0.05;boost=Math.min(Math.max(boost,0.001),50);
          wrap(W,name,function(t,self,args){
            if(!match(fnText(args[0])))return;
            if(delay!==null&&!isNaN(delay)&&+args[1]!==delay)return;
            var a=Array.prototype.slice.call(args);a[1]=(+args[1]||0)*boost;return {args:a};});}
        lib['nano-setTimeout-booster']=function(n,d,b){boostTimer('setTimeout',n,d,b);};
        lib['nano-setInterval-booster']=function(n,d,b){boostTimer('setInterval',n,d,b);};

        lib['addEventListener-defuser']=function(type,handler){
          if(!type&&!handler)return;
          var typeHit=matcher(type),handlerHit=matcher(handler);
          wrap(EventTarget.prototype,'addEventListener',function(t,self,args){
            if(typeHit(String(args[0]))&&handlerHit(fnText(args[1])))return {skip:true,value:undefined};});};

        function parseProps(raw){
          var out=[];String(raw).split(/\s+/).forEach(function(tok){
            if(!tok)return;var i=tok.indexOf(':'),key='url',val=tok;
            if(i>0&&/^[a-z]+$/i.test(tok.slice(0,i))){key=tok.slice(0,i);val=tok.slice(i+1);}
            out.push([key,matcher(val)]);});
          return out;}
        function propsMatch(props,details){
          for(var i=0;i<props.length;i++){var v=details[props[i][0]];if(v===undefined||!props[i][1](String(v)))return false;}
          return true;}

        lib['no-fetch-if']=function(raw){
          if(!raw||typeof W.fetch!=='function')return;
          var props=raw==='*'?[]:parseProps(raw);
          wrap(W,'fetch',function(t,self,args){
            var input=args[0],init=args[1]||{};
            var d={url:typeof input==='string'?input:(input&&input.url)||String(input),
              method:String(init.method||(input&&input.method)||'GET')};
            if(!propsMatch(props,d))return;
            return {skip:true,value:Promise.resolve(new Response('',{status:200,statusText:'OK'}))};});};

        lib['no-xhr-if']=function(raw){
          if(!raw||!W.XMLHttpRequest)return;
          var props=raw==='*'?[]:parseProps(raw),blocked=new WeakSet(),P=XMLHttpRequest.prototype;
          wrap(P,'open',function(t,self,args){
            if(propsMatch(props,{method:String(args[0]||'GET'),url:String(args[1]||'')}))blocked.add(self);else blocked.delete(self);});
          wrap(P,'send',function(t,self){
            if(!blocked.has(self))return;
            var x=self,json=x.responseType==='json';
            try{Object.defineProperties(x,{readyState:{value:4,configurable:true},status:{value:200,configurable:true},
              statusText:{value:'OK',configurable:true},responseText:{value:'',configurable:true},
              response:{value:json?null:'',configurable:true}});}catch(e){}
            nativeTimeout(function(){['readystatechange','load','loadend'].forEach(function(n){try{x.dispatchEvent(new Event(n));}catch(e){}});},1);
            return {skip:true,value:undefined};});};

        lib['no-window-open-if']=function(a,b){
          var raw=a==null?'':String(a);
          if(raw==='0'||raw==='1')raw=(raw==='1'?'!':'')+(b||'');
          var match=matcher(raw);
          wrap(W,'open',function(t,self,args){if(match(String(args[0]==null?'':args[0])))return {skip:true,value:null};});};

        lib['json-prune']=function(pruneRaw,needRaw){
          var prune=String(pruneRaw||'').split(/\s+/).filter(Boolean);if(!prune.length)return;
          var need=String(needRaw||'').split(/\s+/).filter(Boolean);
          function find(obj,path,cb){var parts=path.split('.');
            (function step(o,i){
              if(!isObj(o))return;var k=parts[i],lastPart=i===parts.length-1;
              if(k==='[]'||k==='*'){Object.keys(o).forEach(function(key){if(lastPart)cb(o,key);else step(o[key],i+1);});return;}
              if(!Object.prototype.hasOwnProperty.call(o,k))return;
              if(lastPart)cb(o,k);else step(o[k],i+1);})(obj,0);}
          function has(obj,path){var hit=false;find(obj,path,function(){hit=true;});return hit;}
          function apply(obj){
            try{if(need.length&&!need.every(function(p){return has(obj,p);}))return obj;
              prune.forEach(function(p){find(obj,p,function(o,k){delete o[k];});});}catch(e){}
            return obj;}
          wrap(JSON,'parse',function(t,self,args){return {skip:true,value:apply(Reflect.apply(t,self,args))};});
          if(W.Response)wrap(Response.prototype,'json',function(t,self,args){return {skip:true,value:Reflect.apply(t,self,args).then(apply)};});};

        function domTask(selector,each){
          try{document.querySelector(selector);}catch(e){return;}
          var pending=false;
          function run(){pending=false;try{document.querySelectorAll(selector).forEach(each);}catch(e){}}
          function schedule(){if(!pending){pending=true;nativeTimeout(run,50);}}
          function start(){run();try{new MutationObserver(schedule).observe(document.documentElement,{childList:true,subtree:true,attributes:true});}catch(e){}}
          if(document.readyState==='loading')document.addEventListener('DOMContentLoaded',start,{once:true});else start();}
        lib['remove-attr']=function(attrs,selector){
          var list=String(attrs||'').split(/\s*\|\s*/).filter(Boolean);if(!list.length)return;
          domTask(selector||list.map(function(a){return '['+a+']';}).join(','),function(el){list.forEach(function(a){el.removeAttribute(a);});});};
        lib['remove-class']=function(classes,selector){
          var list=String(classes||'').split(/\s*\|\s*/).filter(Boolean);if(!list.length)return;
          var esc=W.CSS&&CSS.escape?CSS.escape:function(s){return s;};
          domTask(selector||list.map(function(c){return '.'+esc(c);}).join(','),function(el){list.forEach(function(c){el.classList.remove(c);});});};

        function setStorage(kind,key,raw){
          if(!key||raw==null)return;var store;try{store=W[kind];}catch(e){return;}if(!store)return;
          raw=String(raw);
          if(raw==='$remove$'){try{store.removeItem(key);}catch(e){}return;}
          var ok={'undefined':1,'false':1,'true':1,'null':1,'':1,'yes':1,'no':1,'on':1,'off':1,'accept':1,'reject':1,'accepted':1,'rejected':1};
          var v;
          if(raw==='emptyArr')v='[]';else if(raw==='emptyObj')v='{}';
          else if(Object.prototype.hasOwnProperty.call(ok,raw))v=raw;
          else if(/^\d+$/.test(raw)&&+raw<=32767)v=raw;else return;
          try{store.setItem(key,v);}catch(e){}}
        lib['set-local-storage-item']=function(k,v){setStorage('localStorage',k,v);};
        lib['set-session-storage-item']=function(k,v){setStorage('sessionStorage',k,v);};

        lib['noeval-if']=function(needle){
          var match=matcher(needle);
          wrap(W,'eval',function(t,self,args){if(match(String(args[0])))return {skip:true,value:undefined};});};

        lib['nowebrtc']=function(){
          function FakeConnection(){}
          var p=FakeConnection.prototype,noop=function(){},resolved=function(){return Promise.resolve();};
          ['close','addEventListener','removeEventListener','addTrack','removeTrack'].forEach(function(m){p[m]=noop;});
          ['createOffer','createAnswer','setLocalDescription','setRemoteDescription','addIceCandidate','getStats'].forEach(function(m){p[m]=resolved;});
          p.createDataChannel=function(){return {close:noop,send:noop,addEventListener:noop,removeEventListener:noop};};
          ['RTCPeerConnection','webkitRTCPeerConnection'].forEach(function(n){if(n in W){try{W[n]=FakeConnection;}catch(e){}}});};

        lib['set-cookie']=function(name,raw,path){
          if(!name||raw==null)return;raw=String(raw);
          var ok={'true':1,'false':1,'yes':1,'no':1,'y':1,'n':1,'on':1,'off':1,'ok':1,'accept':1,'reject':1,'accepted':1,'rejected':1,'allow':1,'deny':1,'':1};
          if(!Object.prototype.hasOwnProperty.call(ok,raw)&&!(/^\d+$/.test(raw)&&+raw<=32767))return;
          var key=encodeURIComponent(name);
          if((';'+document.cookie).indexOf('; '+key+'='+encodeURIComponent(raw))!==-1||document.cookie.indexOf(key+'='+encodeURIComponent(raw))===0)return;
          var where=path==='none'?'':'; path='+(path&&path.charAt(0)==='/'?path:'/');
          try{document.cookie=key+'='+encodeURIComponent(raw)+where;}catch(e){}};

        function detectorStub(){
          function Detector(){this._clean=[];}
          var p=Detector.prototype;
          p.setOption=function(){return this;};p.emitEvent=function(){return this;};
          p.clearEvent=function(){this._clean=[];return this;};
          p.check=function(){this._clean.forEach(function(f){try{f();}catch(e){}});return true;};
          p.on=function(detected,fn){if(!detected&&typeof fn==='function'){this._clean.push(fn);nativeTimeout(function(){try{fn();}catch(e){}},1);}return this;};
          p.onDetected=function(){return this;};
          p.onNotDetected=function(fn){return this.on(false,fn);};
          return Detector;}
        function lockDetectors(classNames,instanceNames){
          var Detector=detectorStub(),instance=new Detector();
          classNames.forEach(function(n){try{Object.defineProperty(W,n,{configurable:true,get:function(){return Detector;},set:function(){}});}catch(e){}});
          instanceNames.forEach(function(n){try{Object.defineProperty(W,n,{configurable:true,get:function(){return instance;},set:function(){}});}catch(e){}});}
        lib['nofab']=function(){lockDetectors(['FuckAdBlock','SniffAdBlock'],['fuckAdBlock','sniffAdBlock']);};
        lib['nobab']=function(){lockDetectors(['BlockAdBlock'],['blockAdBlock']);};
        """;
}
