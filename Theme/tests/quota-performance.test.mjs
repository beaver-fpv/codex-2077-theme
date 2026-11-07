import test from 'node:test';
import assert from 'node:assert/strict';
import vm from 'node:vm';
import {readFileSync} from 'node:fs';

const source=readFileSync(new URL('../runtime/quota-ram.js',import.meta.url),'utf8');
function environment(){
  const observers=[],timeouts=new Map(),intervals=new Map(),events=new Map();
  let next=0,menu=null,scans=0;
  const body={},root={dataset:{heigeCodexSkin:'deep-dive-protocol'}};
  const document={body,documentElement:root,hidden:false,
    querySelector:()=>menu,
    querySelectorAll(selector){if(selector==='[role="menu"]'){scans++;return menu?[menu]:[];}return [];},
    addEventListener:(name,fn)=>events.set(name,fn),removeEventListener:name=>events.delete(name)};
  class MutationObserver {
    constructor(fn){this.callback=fn;this.target=null;observers.push(this);}
    observe(target){this.target=target;}
    disconnect(){this.target=null;}
  }
  const window={};
  vm.runInNewContext(source,{window,document,MutationObserver,
    setTimeout:fn=>{timeouts.set(++next,fn);return next;},clearTimeout:id=>timeouts.delete(id),
    setInterval:fn=>{intervals.set(++next,fn);return next;},clearInterval:id=>intervals.delete(id)});
  return {window,document,observers,timeouts,intervals,events,
    openMenu(items=[]){menu={querySelectorAll:()=>items};return menu;},
    scans:()=>scans,tick(){for(const fn of intervals.values())fn();},
    flush(){const pending=[...timeouts.values()];timeouts.clear();for(const fn of pending)fn();}};
}
test('streaming chat has no subtree observer or closed-menu rescan',()=>{
  const env=environment(),before=env.scans();
  assert(!env.observers.some(o=>o.target===env.document.body));
  for(let i=0;i<100;i++)env.tick();
  env.flush();
  assert.equal(env.scans(),before);
  assert.equal(env.timeouts.size,0);
});
test('menu changes are coalesced, hidden window sleeps, removal cancels all work',()=>{
  const env=environment(),menu=env.openMenu();
  env.tick();
  assert.equal(env.observers[0].target,menu);
  for(let i=0;i<100;i++)env.observers[0].callback();
  assert.equal(env.timeouts.size,1);
  env.flush();
  const before=env.scans();
  env.document.hidden=true;env.tick();env.flush();
  assert.equal(env.scans(),before);
  env.document.hidden=false;env.tick();
  env.window.__codexQuotaRAM.dispose();
  assert.equal(env.timeouts.size,0);assert.equal(env.intervals.size,0);
  assert.equal(env.events.size,0);
  assert(env.observers.every(o=>o.target===null));
});
test('ordinary menu entries and owned bars never force a layout read',()=>{
  const env=environment();
  const rect=()=>{throw Error('Unexpected layout read');};
  env.openMenu([
    {textContent:'Settings',closest:()=>null,getBoundingClientRect:rect},
    {textContent:'5 小时 20%',closest:()=>({}),getBoundingClientRect:rect},
  ]);
  env.tick();env.flush();
  env.window.__codexQuotaRAM.dispose();
});
