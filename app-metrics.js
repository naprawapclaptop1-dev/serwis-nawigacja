(()=>{
'use strict';
const REPO='naprawapclaptop1-dev/serwis-nawigacja';
const CACHE_KEY='drp.portal.metrics.v2';
const CACHE_TTL=15*60*1000;
const byId=id=>document.getElementById(id);
const number=value=>new Intl.NumberFormat('pl-PL').format(value);
const set=(id,text)=>{const el=byId(id);if(el)el.textContent=text;};
const stableTag=tag=>/^v?\d+\.\d+\.\d+(?:\.\d+)?$/.test(String(tag||''));
function compareTags(a,b){
  const pa=String(a).replace(/^v/i,'').split('.').map(x=>Number(x)||0);
  const pb=String(b).replace(/^v/i,'').split('.').map(x=>Number(x)||0);
  for(let i=0;i<Math.max(pa.length,pb.length);i++){const d=(pa[i]||0)-(pb[i]||0);if(d)return d;}
  return 0;
}
function installerAsset(release){
  return (release.assets||[]).find(asset=>String(asset.name||'').toLowerCase()==='diskrecoverypro_setup.exe' && /^https:\/\/github\.com\//i.test(asset.browser_download_url||''));
}
function showMetrics(data,stale=false){
  set('installerDownloads',number(data.total));
  set('latestDownloads',data.latest?number(data.latest.count)+' · '+data.latest.tag:'Brak publicznego instalatora');
  set('downloadCounterStatus',stale?'Dane z ostatniego zapisu · GitHub chwilowo nie odpowiada.':'Dane z GitHub Releases · liczba pobrań, nie liczba unikalnych instalacji.');
  if(data.latest&&data.latest.url){
    for(const link of document.querySelectorAll('[data-download-installer]')){
      link.href=data.latest.url;
      link.setAttribute('data-release-tag',data.latest.tag);
      link.setAttribute('aria-label','Pobierz oficjalny instalator Disk Recovery PRO '+data.latest.tag);
    }
  }
}
function loadCached(){
  try{const cached=JSON.parse(localStorage.getItem(CACHE_KEY)||'null');if(cached&&cached.savedAt&&Date.now()-cached.savedAt<7*24*60*60*1000&&Number.isFinite(cached.total)&&Object.prototype.hasOwnProperty.call(cached,'latest')){showMetrics(cached,Date.now()-cached.savedAt>=CACHE_TTL);return cached;} }catch(_){}
  return null;
}
async function fetchReleases(){
  const all=[];
  for(let page=1;page<=10;page++){
    const response=await fetch('https://api.github.com/repos/'+REPO+'/releases?per_page=100&page='+page,{
      headers:{Accept:'application/vnd.github+json','X-GitHub-Api-Version':'2022-11-28'},
      cache:'default'
    });
    if(!response.ok)throw new Error('GitHub HTTP '+response.status);
    const rows=await response.json();if(!Array.isArray(rows))throw new Error('Nieprawidłowa odpowiedź GitHub API');
    all.push(...rows);if(rows.length<100)break;
  }
  return all;
}
async function loadDownloads(){
  const old=loadCached();
  if(old&&Date.now()-old.savedAt<CACHE_TTL)return;
  try{
    const releases=await fetchReleases();
    const stable=releases.filter(r=>!r.draft&&!r.prerelease&&stableTag(r.tag_name)&&installerAsset(r));
    stable.sort((a,b)=>compareTags(b.tag_name,a.tag_name));
    let total=0;
    for(const release of stable){const asset=installerAsset(release);total+=Math.max(0,Number(asset.download_count)||0);}
    const latestRelease=stable[0]||null;const latestAsset=latestRelease?installerAsset(latestRelease):null;
    const data={total,latest:latestRelease&&latestAsset?{tag:latestRelease.tag_name,count:Math.max(0,Number(latestAsset.download_count)||0),url:latestAsset.browser_download_url}:null,savedAt:Date.now()};
    showMetrics(data,false);try{localStorage.setItem(CACHE_KEY,JSON.stringify(data));}catch(_){}
  }catch(_){
    if(old)return;
    set('installerDownloads','Niedostępne');set('latestDownloads','Statystyka chwilowo niedostępna');
    set('downloadCounterStatus','Nie udało się pobrać danych z GitHub Releases. Spróbuj ponownie później.');
  }
}
function renderReview(review){
  const article=document.createElement('article');article.className='review-item';
  const heading=document.createElement('div');heading.className='review-heading';
  const name=document.createElement('strong');name.textContent=String(review.nickname||'Użytkownik').slice(0,40);
  const rating=document.createElement('span');const n=Math.min(5,Math.max(1,Number(review.rating)||1));rating.textContent='★'.repeat(n)+'☆'.repeat(5-n);rating.setAttribute('aria-label','Ocena '+n+' na 5');
  heading.append(name,rating);const body=document.createElement('p');body.textContent=String(review.text||'').slice(0,1000);
  const date=document.createElement('small');date.textContent=review.createdUtc?new Date(review.createdUtc).toLocaleDateString('pl-PL'):'';
  article.append(heading,body,date);return article;
}
async function loadReviews(api){
  const list=byId('appReviewList');if(!list)return;
  const submit=byId('reviewSubmit');
  if(!api){set('reviewSummary','Opinie oczekują na konfigurację serwera.');set('reviewStatus','Formularz opinii zostanie uruchomiony po podłączeniu serwera HTTPS. Nie zapisujemy opinii wyłącznie w przeglądarce.');if(submit)submit.disabled=true;return;}
  try{
    const response=await fetch(api+'/api/app-reviews?appId=disk-recovery-pro',{headers:{Accept:'application/json'},cache:'no-store'});
    if(!response.ok)throw new Error('HTTP '+response.status);const result=await response.json();
    set('reviewSummary',result.count?('Średnia '+Number(result.average).toFixed(2).replace('.',',')+'/5 · '+number(result.count)+' zatwierdzonych opinii'):'Brak zatwierdzonych opinii');
    list.replaceChildren();for(const review of (result.reviews||[]).slice(0,30))list.append(renderReview(review));
    if(!list.childElementCount){const empty=document.createElement('p');empty.className='note';empty.textContent='Nie ma jeszcze zatwierdzonych opinii. Możesz dodać pierwszą ocenę.';list.append(empty);}
    set('reviewStatus','Opinie są moderowane przed publikacją.');if(submit)submit.disabled=false;
  }catch(_){set('reviewSummary','Opinie chwilowo niedostępne.');set('reviewStatus','Serwer opinii jest chwilowo niedostępny.');if(submit)submit.disabled=true;}
}
async function init(){
  loadDownloads();
  let config={};try{const r=await fetch('./license-config.json',{cache:'no-store'});if(r.ok)config=await r.json();}catch(_){}
  const candidate=String(config.reviewsApiUrl||config.checkoutApi||'').trim().replace(/\/+$/,'');
  const api=/^https:\/\//i.test(candidate)?candidate:'';
  await loadReviews(api);
  const form=byId('appReviewForm');if(form){form.dataset.openedAt=String(Date.now());form.addEventListener('submit',async event=>{
    event.preventDefault();if(!api){set('reviewStatus','Publiczny serwer opinii nie jest skonfigurowany.');return;}
    const nickname=String(byId('reviewNickname')?.value||'').trim();const rating=Number(byId('reviewRating')?.value||5);const text=String(byId('reviewText')?.value||'').trim();
    const website=String(byId('reviewWebsite')?.value||'').trim();const consent=!!byId('reviewConsent')?.checked;
    if(website){set('reviewStatus','Dziękujemy za wiadomość.');return;}
    if(Date.now()-Number(form.dataset.openedAt||Date.now())<3000){set('reviewStatus','Odczekaj chwilę i sprawdź treść opinii przed wysłaniem.');return;}
    if(text.length<20||text.length>1000){set('reviewStatus','Opinia musi mieć od 20 do 1000 znaków.');return;}
    if(!consent){set('reviewStatus','Zaznacz zgodę na publikację nazwy wyświetlanej i treści po zatwierdzeniu.');return;}
    const button=byId('reviewSubmit');if(button)button.disabled=true;set('reviewStatus','Wysyłam opinię do moderacji…');
    try{
      const response=await fetch(api+'/api/app-reviews',{method:'POST',headers:{'Content-Type':'application/json','Accept':'application/json'},body:JSON.stringify({appId:'disk-recovery-pro',nickname,rating,text,publicConsent:consent,website})});
      const data=await response.json().catch(()=>({}));if(!response.ok)throw new Error(data.error||data.detail||('HTTP '+response.status));
      form.reset();form.dataset.openedAt=String(Date.now());set('reviewStatus','Dziękujemy. Opinia została przyjęta i oczekuje na moderację.');
    }catch(error){set('reviewStatus','Nie udało się wysłać opinii: '+error.message);}
    finally{if(button)button.disabled=false;}
  });}
}
if(document.readyState==='loading')document.addEventListener('DOMContentLoaded',init);else init();
})();
