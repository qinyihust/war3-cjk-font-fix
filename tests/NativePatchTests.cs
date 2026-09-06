// Isolated x86 execution tests of the embedded revision-2 stubs.
// Engine dependencies are stand-ins. No game process is attached or modified.
// With an optional local Game.dll, verify and derive the same patched bytes in memory.
using System;
using System.Linq;
using System.Reflection;
using War3FontFix;
using System.IO;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
class NativePatchTests {
 [DllImport("kernel32.dll",SetLastError=true)] static extern IntPtr VirtualAlloc(IntPtr p,UIntPtr n,uint a,uint protect);
 [DllImport("kernel32.dll")] static extern bool VirtualFree(IntPtr p,UIntPtr n,uint t);
 [DllImport("kernel32.dll")] static extern bool FlushInstructionCache(IntPtr process,IntPtr address,UIntPtr size);
 [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate void Run();
 static IntPtr mem; static int addr;
 static int batchRva,atlasRva,rebuildRva,renderRva,memsetRva,continuationRva;
 static bool old;
 static int A(int r){return addr+r;}
 static void W(int r,int v){Marshal.WriteInt32(mem,r,v);}
 static int R(int r){return Marshal.ReadInt32(mem,r);}
 static byte[] Hex(string s){byte[] b=new byte[s.Length/2];for(int i=0;i<b.Length;i++)b[i]=Convert.ToByte(s.Substring(2*i,2),16);return b;}
 static void Code(int r,byte[] bytes){Marshal.Copy(bytes,0,new IntPtr(A(r)),bytes.Length);}
 static void Check(bool condition,string what){if(!condition)throw new Exception(what);}
 static void I(List<byte>b,int n){b.AddRange(BitConverter.GetBytes(n));}
 static void Store(List<byte>b,int reg,int destination){b.Add(0x89);b.Add((byte)(0x05+reg*8));I(b,A(destination));}
 static void Harness(int target,int eax,int ebx,int ecx){
  var b=new List<byte>(Hex("9c60"));
  // All caller registers are saved before the controlled input register pattern.
  int[] values={eax,ecx,0x12345678,ebx,0,0x23456789,0x3456789a,0x456789ab};
  for(int reg=0;reg<8;reg++){if(reg==4)continue;b.Add((byte)(0xb8+reg));I(b,values[reg]);}
  Store(b,4,0x2100);b.AddRange(Hex("f9")); // Known carry flag; direction remains clear.
  b.Add(0xe8);I(b,A(target)-(A(0x1000)+b.Count+4));
  for(int reg=0;reg<8;reg++)Store(b,reg,0x2000+4*reg);
  b.AddRange(Hex("9c58"));Store(b,0,0x2020);b.AddRange(Hex("619dc3"));
  Code(0x1000,b.ToArray());FlushInstructionCache(new IntPtr(-1),mem,new UIntPtr(0x1000000));
  ((Run)Marshal.GetDelegateForFunctionPointer(new IntPtr(A(0x1000)),typeof(Run)))();
  for(int reg=0;reg<8;reg++){if(reg==4)Check(R(0x2000+reg*4)==R(0x2100),"stack balance");else Check(R(0x2000+reg*4)==values[reg],"register "+reg+" preservation");}
  Check((R(0x2020)&1)==1,"carry flag preservation");
 }
 static void AtlasHarness(){
  // Each compiler family has a different destination for the original load.
  var b=new List<byte>(Hex("9c60"));
  int[] values={A(0x6000),old?0x77777777:0x20,0x12345678,A(0x7000),0,0x23456789,old?0x20:0x3456789a,0x456789ab};
  for(int reg=0;reg<8;reg++){if(reg==4)continue;b.Add((byte)(0xb8+reg));I(b,reg==(old?6:1)?0x77777777:values[reg]);}
  Store(b,4,0x2100);b.Add(0xf9);b.Add(0xe8);I(b,A(atlasRva)-(A(0x1000)+b.Count+4));
  for(int reg=0;reg<8;reg++)Store(b,reg,0x2000+reg*4);
  b.AddRange(Hex("9c58"));Store(b,0,0x2020);b.AddRange(Hex("619dc3"));Code(0x1000,b.ToArray());
  FlushInstructionCache(new IntPtr(-1),mem,new UIntPtr(0x1000000));
  ((Run)Marshal.GetDelegateForFunctionPointer(new IntPtr(A(0x1000)),typeof(Run)))();
  for(int reg=0;reg<8;reg++)Check(R(0x2000+reg*4)==(reg==4?R(0x2100):values[reg]),"atlas register/stack "+reg);
  Check((R(0x2020)&1)==1,"atlas carry flag preservation");
 }
 static int Main(string[] args){try{
  Check(IntPtr.Size==4,"must run as x86");
  Check(args.Length<=1,"Usage: NativePatchTests.exe [Game.dll]");
  var catalog=PatchCatalog.Embedded(Assembly.GetExecutingAssembly());
  byte[] source=args.Length==1?File.ReadAllBytes(args[0]):null;
  var profiles=source==null?catalog.Profiles:new[]{catalog.Find(Bytes.Hash(source),source.Length)};
  foreach(var profile in profiles){
  Check(profile!=null,"supported DLL fingerprint");
  byte[] game=source==null?null:PatchEngine.Apply(source,profile);
  var batch=profile.patches.Single(p=>p.id=="batch-rebuild-stub");
  var atlas=profile.patches.Single(p=>p.id=="atlas-clear-stub");
  byte[] batchCode=Bytes.FromHex(batch.replacement),atlasCode=Bytes.FromHex(atlas.replacement);
  batchRva=batch.rva;atlasRva=atlas.rva;
  rebuildRva=batchRva+37+BitConverter.ToInt32(batchCode,33);
  renderRva=batchRva+batchCode.Length+BitConverter.ToInt32(batchCode,batchCode.Length-4);
  old=atlasCode[2]==0xfc;
  memsetRva=old?0:atlasRva+17+BitConverter.ToInt32(atlasCode,13);
  continuationRva=atlasRva+atlasCode.Length+BitConverter.ToInt32(atlasCode,atlasCode.Length-4);
  mem=VirtualAlloc(IntPtr.Zero,new UIntPtr(0x1000000),0x3000,0x40);Check(mem!=IntPtr.Zero,"VirtualAlloc");addr=mem.ToInt32();
  foreach(var patch in profile.Sites(profile.currentRevision).Where(p=>p.id=="batch-rebuild-stub"||p.id=="atlas-clear-stub")) {
   byte[] code=Bytes.FromHex(patch.replacement);
   if(game!=null)Check(game.Skip(patch.fileOffset).Take(code.Length).SequenceEqual(code),"DLL/stub agreement");
   Code(patch.rva,code);
  }
  // Original render stub: return without touching state. Rebuild stub increments a per-string counter,
  // and may invalidate that same string again to model an eviction during reconstruction.
  Code(renderRva,Hex("c3"));Code(rebuildRva,Hex("ff818400000083b98800000000740ac7818000000001000000c3"));
  W(0x301c,0x10);W(0x3024,-1);Harness(batchRva,0x11223344,0x55667788,A(0x3000));
  W(0x3024,0);Harness(batchRva,0x11223344,0x55667788,A(0x3000));
  int[] nodes={0x4000,0x4200,0x4400,0x4600};
  for(int i=0;i<nodes.Length;i++){W(nodes[i]+0x14,i+1<nodes.Length?A(nodes[i+1]):-1);W(nodes[i]+0x80,i==1?0:1);}
  W(0x4688,1);W(0x3024,A(nodes[0]));Harness(batchRva,0x11223344,0x55667788,A(0x3000));
  for(int i=0;i<nodes.Length;i++){Check(R(nodes[i]+0x84)==(i==1?0:1),"rebuild count "+i);Check(R(nodes[i]+0x80)==(i==3?1:0),"dirty flag "+i);}
  Harness(batchRva,0x11223344,0x55667788,A(0x3000));Check(R(0x4684)==2,"re-invalidated string retries on next render");
  // cdecl memset stand-in: preserve EDI, fill exactly the requested count.
  if(!old)Code(memsetRva,Hex("578b7c24088b44240c8b4c2410f3aa5fc3"));Code(continuationRva,Hex("c3"));
  W(0x61b0,0x20);W(0x7004,A(0x100100));byte[] fill=new byte[0x40020];for(int i=0;i<fill.Length;i++)fill[i]=0xa5;Code(0x1000f0,fill);
  AtlasHarness();byte[] got=new byte[fill.Length];Marshal.Copy(new IntPtr(A(0x1000f0)),got,0,got.Length);
  for(int i=0;i<got.Length;i++)Check(got[i]==(i<16||i>=16+0x40000?0xa5:0),"atlas write bounds "+i);
  Console.WriteLine("PASS "+profile.gameVersion+": x86 stubs; empty/null batch; selective rebuild; re-eviction; repeat render; registers/flags/stack; original load; 262144-byte clear with guards.");
  VirtualFree(mem,UIntPtr.Zero,0x8000);mem=IntPtr.Zero;
  }return 0;
 }catch(Exception e){Console.Error.WriteLine(e);return 1;}finally{if(mem!=IntPtr.Zero)VirtualFree(mem,UIntPtr.Zero,0x8000);}}
}
