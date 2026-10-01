const KEY='alumunion-marketing-share';
export function getMarketingShareId():string|undefined {
 if(typeof window==='undefined')return;
 const incoming=new URLSearchParams(window.location.search).get('mc');
 if(incoming&&/^[a-f0-9]{32}$/.test(incoming))return incoming;
 try{return sessionStorage.getItem(KEY)||undefined;}catch{return;}
}
export function rememberMarketingShare(id:string){try{sessionStorage.setItem(KEY,id);}catch{/* private browsing may disable storage */}}
