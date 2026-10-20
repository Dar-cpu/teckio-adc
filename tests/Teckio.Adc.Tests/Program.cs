using System.Text;
using Teckio.Adc.Core;
int count=0;
void Check(bool condition,string name){if(!condition)throw new Exception("FAIL: "+name);Console.WriteLine("PASS "+name);count++;}
void Near(double a,double b,double tol,string name)=>Check(Math.Abs(a-b)<tol,name);
Check(Wire.Crc(Encoding.ASCII.GetBytes("123456789"))==0x29B1,"CRC16/CCITT-FALSE reference");
byte[] encoded=Wire.Encode(0x12,0x1234,Wire.Words(0x182060,0x140));
var decoder=new PacketDecoder();List<Packet> decoded=[];
foreach(byte b in encoded)decoded.AddRange(decoder.Feed([b]));
Check(decoded.Count==1&&decoded[0].Sequence==0x1234&&decoded[0].Payload.Length==8,"fragmented CDC frame");
byte[] broken=(byte[])encoded.Clone();broken[15]^=1;
decoded=decoder.Feed([0,1,0x54,..broken,..encoded,..encoded]).ToList();
Check(decoded.Count==2&&decoder.CorruptFrames>0,"CRC rejection + stream resynchronisation");
var random=new Random(7195);decoder=new();int expected=0,actual=0;
for(int i=0;i<1000;i++){
    byte[] payload=new byte[random.Next(0,55)];random.NextBytes(payload);
    byte[] f=Wire.Encode(1,(ushort)i,payload);expected++;
    int cut=random.Next(1,64);actual+=decoder.Feed(f.AsSpan(0,cut)).Count();actual+=decoder.Feed(f.AsSpan(cut)).Count();
}
Check(expected==actual,"1000 randomly fragmented packets");
var profile=new Ad7195Profile();
Near(profile.Convert(0x800000,0x140,5),0,1e-15,"bipolar zero");
Near(profile.Convert(0,0x140,5),-5,1e-15,"bipolar negative rail");
Near(profile.Convert(0x800000,0x148,5),2.5,1e-15,"unipolar midpoint");
Check(profile.Validate(0x180060,0x141)!="","reserved gain rejected");
Check(profile.Validate(0x180060,0x400140)!="","ACX without chop rejected");
Check(profile.Validate(0x180060,0x1c0)!="","burnout without buffer rejected");
Check(profile.Validate(0x180000,0x140)!="","zero FS rejected");
Check(profile.Validate(0x182060,0x140)=="","valid operating configuration");
var stats=Analysis.Compute([1,2,3],[0,0.5,1],10);
Near(stats.Mean,2,1e-12,"Welford mean");Near(stats.Sigma,1,1e-12,"sample sigma");Near(stats.Rate,2,1e-12,"measured sample rate");
Check(double.IsNaN(Analysis.Compute([1,1],[0,1],10).NoiseFreeBits),"constant input is not infinite resolution");
int n=4096;double[] sine=Enumerable.Range(0,n).Select(i=>Math.Sin(2*Math.PI*128*i/4096)).ToArray();
var fft=Analysis.Fft(sine,4096,2);Near(fft.PeakHz,128,0.01,"FFT known tone");Near(fft.Dbfs[128],0,0.02,"Hann coherent gain");
var histogram=Analysis.Histogram([1,2,3,4,5],4);Near(histogram.Y.Sum(),5,0.01,"histogram retains all samples");
using(var session=new Session(new(DateTimeOffset.UtcNow,false,"test",0x180060,0x140,5),3)){
    session.Add(new(0,uint.MaxValue-10,0x800000,0),0);session.Add(new(1,19,0x800000,0),0);
    session.Add(new(3,49,0xffffff,0x40),1);session.Add(new(4,79,0x800000,0),1);
    var c=session.Snapshot();Check(c.Received==4&&c.Evicted==1&&c.Samples.Length==3,"bounded sample ring");
    Check(c.IndexGaps==1&&c.Invalid==1&&c.FirmwareDrops==1,"loss and invalid accounting");
    Near(c.Samples[0].Seconds,30/3000000.0,1e-12,"32-bit timestamp wrap");
}
Console.WriteLine($"{count} tests passed");
