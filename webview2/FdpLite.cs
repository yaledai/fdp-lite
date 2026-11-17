using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Security.Cryptography;
using System.IO.Pipes;
using System.Threading;
using System.Security.Principal;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Win32;
using System.Diagnostics;
using System.Reflection;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

[assembly: AssemblyVersion("1.0.8.0")]
[assembly: AssemblyFileVersion("1.0.8.0")]
[assembly: AssemblyTitle("FDP 极简版 1.0.8")]
[assembly: AssemblyProduct("Folio Dot Pig")]

class FdpLite : Form {
  static string DisplayName { get { return "FDP 极简版 " + typeof(FdpLite).Assembly.GetName().Version.ToString(3); } }
  class Recent { public string id {get;set;} public string path {get;set;} }
  [DllImport("user32.dll")] static extern bool ReleaseCapture();
  [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
  [DllImport("shlwapi.dll", CharSet=CharSet.Unicode)] static extern int AssocQueryString(uint flags,uint str,string assoc,string extra,StringBuilder result,ref uint length);
  [DllImport("shell32.dll")] static extern void SHChangeNotify(uint eventId,uint flags,IntPtr item1,IntPtr item2);
  static void SetAssociationKey(string key,string name,object value){using(var sub=Registry.CurrentUser.CreateSubKey(key)){if(sub==null)throw new IOException("无法注册默认应用");sub.SetValue(name,value,RegistryValueKind.String);}}
  bool IsDefaultPdf(){try{uint length=0;AssocQueryString(0,2,".pdf",null,null,ref length);if(length==0||length>32768)return false;var result=new StringBuilder((int)length);return AssocQueryString(0,2,".pdf",null,result,ref length)==0&&String.Equals(Path.GetFullPath(result.ToString()),Path.GetFullPath(Application.ExecutablePath),StringComparison.OrdinalIgnoreCase);}catch{return false;}}
  void RegisterPdf(){string exe=Path.GetFullPath(Application.ExecutablePath),icon=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"fdp-document.ico");if(!File.Exists(icon))throw new IOException("找不到 PDF 文件图标");const string cls=@"Software\Classes\FDP.Lite.PDF",app=@"Software\Classes\Applications\FDP-Lite.exe",cap=@"Software\FDP-Lite\Capabilities";SetAssociationKey(cls,"",DisplayName+" PDF 文档");SetAssociationKey(cls+@"\DefaultIcon","","\""+icon+"\",0");SetAssociationKey(cls+@"\shell\open\command","","\""+exe+"\" \"%1\"");SetAssociationKey(app,"FriendlyAppName",DisplayName);SetAssociationKey(app+@"\DefaultIcon","","\""+icon+"\",0");SetAssociationKey(app+@"\shell\open\command","","\""+exe+"\" \"%1\"");SetAssociationKey(app+@"\SupportedTypes",".pdf","");using(var openWith=Registry.CurrentUser.CreateSubKey(@"Software\Classes\.pdf\OpenWithProgids"))openWith.SetValue("FDP.Lite.PDF",new byte[0],RegistryValueKind.None);SetAssociationKey(cap,"ApplicationName",DisplayName);SetAssociationKey(cap,"ApplicationDescription","FDP 极简版 PDF 阅读与编辑");SetAssociationKey(cap+@"\FileAssociations",".pdf","FDP.Lite.PDF");SetAssociationKey(@"Software\RegisteredApplications","FDP 极简版",cap);SHChangeNotify(0x08000000,0,IntPtr.Zero,IntPtr.Zero);}
  const int WM_NCLBUTTONDOWN = 0x00A1, WM_SYSCOMMAND = 0x0112, SC_SIZE = 0xF000, HTCAPTION = 2;
  readonly WebView2 view = new WebView2();
  readonly JavaScriptSerializer json = new JavaScriptSerializer { MaxJsonLength = 32 * 1024 * 1024 };
  readonly Dictionary<string,string> documents = new Dictionary<string,string>();
  class SaveTransfer { public string id,token,target,temp; public long expected,written; public FileStream stream; public bool replace; }
  readonly Dictionary<string,string> documentHashes = new Dictionary<string,string>();
  static string FileHash(string path) { using(var sha=SHA256.Create()) using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read | FileShare.Delete)) return Convert.ToBase64String(sha.ComputeHash(stream)); }
  SaveTransfer transfer;
  bool pageReady; readonly List<string> pendingFiles=new List<string>();
  static string InstanceName { get { return "FDP-Lite-"+WindowsIdentity.GetCurrent().User.Value; } }
  static string PdfArgument(string[] args) { return args.FirstOrDefault(path=>Path.IsPathRooted(path)&&String.Equals(Path.GetExtension(path),".pdf",StringComparison.OrdinalIgnoreCase)&&File.Exists(path)); }
  void AcceptExternal(string path) { if(WindowState==FormWindowState.Minimized)WindowState=FormWindowState.Normal;Activate();if(path==null)return;if(pageReady) { try { ChooseFile(path); } catch(Exception ex) { Send(new {type="error",message=ex.Message}); } } else pendingFiles.Add(path); }
  void ListenForFiles() { var thread=new Thread(()=>{while(!IsDisposed) { try { using(var pipe=new NamedPipeServerStream(InstanceName,PipeDirection.In,1,PipeTransmissionMode.Byte,PipeOptions.None)) { pipe.WaitForConnection();using(var reader=new StreamReader(pipe,Encoding.UTF8)) { string path=reader.ReadLine();if(path!=null && path.Length<32768) { string valid=PdfArgument(new[]{path});if(!IsDisposed&&IsHandleCreated)BeginInvoke((Action)(()=>AcceptExternal(valid))); } } } } catch { if(IsDisposed)return;Thread.Sleep(100); } }});thread.IsBackground=true;thread.Start(); }
  bool presentationFullScreen, closingApproved; System.Drawing.Rectangle beforeFullScreen; FormWindowState beforeFullScreenState;
  readonly string site = "https://fdp.local/"; readonly string documentSite = "https://fdp-document.local/";
  string DataDirectory { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FDP-WebView2-Prototype"); } }
  string RecentFile { get { return Path.Combine(DataDirectory,"recent-pdfs.json"); } }
  static bool ValidId(string id) { Guid parsed; return Guid.TryParse(id,out parsed); }
  List<Recent> ReadRecent() {
    if (!File.Exists(RecentFile)) return new List<Recent>();
    var items=json.Deserialize<List<Recent>>(File.ReadAllText(RecentFile));
    return (items ?? new List<Recent>()).Where(x=>x!=null && !String.IsNullOrWhiteSpace(x.id) && ValidId(x.id) && !String.IsNullOrWhiteSpace(x.path) && Path.IsPathRooted(x.path) && String.Equals(Path.GetExtension(x.path),".pdf",StringComparison.OrdinalIgnoreCase)).Take(10).ToList();
  }
  void WriteRecent(List<Recent> items) {
    Directory.CreateDirectory(DataDirectory); string tmp=Path.Combine(DataDirectory,Guid.NewGuid().ToString("N")+".tmp");
    try { File.WriteAllText(tmp,json.Serialize(items),new UTF8Encoding(false)); if(File.Exists(RecentFile))File.Replace(tmp,RecentFile,null);else File.Move(tmp,RecentFile); }
    finally { if(File.Exists(tmp))File.Delete(tmp); }
  }
  void SendRecent() { Send(new {type="recent-list",items=ReadRecent().Take(5).Select(x=>new {x.id,name=Path.GetFileName(x.path),location=Path.GetDirectoryName(x.path)}).ToArray()}); }
  void ChooseFile(string path) { string token=Guid.NewGuid().ToString("N"); documents[token]=path; documentHashes[token]=FileHash(path); Send(new {type="opened",name=Path.GetFileName(path),url=documentSite+token,token=token}); }
  FdpLite() { Text = "FDP 极简版"; Width = 1280; Height = 850; MinimumSize = new System.Drawing.Size(900,650); FormBorderStyle = FormBorderStyle.None; Padding = new Padding(5); BackColor = System.Drawing.Color.FromArgb(224,238,238); var ico = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fdp-app.ico"); if (File.Exists(ico)) Icon = new System.Drawing.Icon(ico); view.Dock = DockStyle.Fill; Controls.Add(view); Shown += Start; ListenForFiles(); Resize += delegate { Padding = new Padding(WindowState == FormWindowState.Maximized ? 0 : 5); if (view.CoreWebView2 != null) Send(new { type="window-state", maximized=WindowState == FormWindowState.Maximized }); }; MouseMove += ResizeCursor; MouseDown += ResizeStart; }
  int ResizeEdge(System.Drawing.Point p) { if (WindowState == FormWindowState.Maximized) return 0; bool l=p.X<6, r=p.X>=ClientSize.Width-6, t=p.Y<6, b=p.Y>=ClientSize.Height-6; if(l&&t)return 4; if(r&&t)return 5; if(l&&b)return 7; if(r&&b)return 8; if(l)return 1; if(r)return 2; if(t)return 3; if(b)return 6; return 0; }
  void ResizeCursor(object sender, MouseEventArgs e) { int edge=ResizeEdge(e.Location); Cursor = edge==1||edge==2 ? Cursors.SizeWE : edge==3||edge==6 ? Cursors.SizeNS : edge==4||edge==8 ? Cursors.SizeNWSE : edge==5||edge==7 ? Cursors.SizeNESW : Cursors.Default; }
  void ResizeStart(object sender, MouseEventArgs e) { int edge=ResizeEdge(e.Location); if(e.Button==MouseButtons.Left && edge!=0){ReleaseCapture();SendMessage(Handle,WM_SYSCOMMAND,(IntPtr)(SC_SIZE+edge),IntPtr.Zero);} }
  void ToggleMaximize() { if(WindowState == FormWindowState.Maximized) WindowState=FormWindowState.Normal; else { MaximizedBounds=Screen.FromControl(this).WorkingArea; WindowState=FormWindowState.Maximized; } }
  async void Start(object sender, EventArgs e) {
    try {
      var userData = DataDirectory;
      var environment = await CoreWebView2Environment.CreateAsync(null, userData);
      await view.EnsureCoreWebView2Async(environment);
      var core = view.CoreWebView2;
      core.Settings.AreDevToolsEnabled = false; core.Settings.AreDefaultContextMenusEnabled = false;
      core.SetVirtualHostNameToFolderMapping("fdp.local", Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"site"), CoreWebView2HostResourceAccessKind.DenyCors);
      core.AddWebResourceRequestedFilter("https://fdp-document.local/*", CoreWebView2WebResourceContext.All, CoreWebView2WebResourceRequestSourceKinds.All);
      core.AddWebResourceRequestedFilter("https://fdp-font.local/*", CoreWebView2WebResourceContext.All, CoreWebView2WebResourceRequestSourceKinds.All);
      core.WebResourceRequested += Resource;
      core.WebMessageReceived += Message;
      core.ContainsFullScreenElementChanged += delegate {
        if (core.ContainsFullScreenElement && !presentationFullScreen) { beforeFullScreenState=WindowState; beforeFullScreen=WindowState==FormWindowState.Maximized?RestoreBounds:Bounds; presentationFullScreen=true; var screen=Screen.FromControl(this); WindowState=FormWindowState.Normal; Bounds=screen.Bounds; Padding=new Padding(0); }
        else if (!core.ContainsFullScreenElement && presentationFullScreen) { presentationFullScreen=false; WindowState=FormWindowState.Normal; Bounds=beforeFullScreen; WindowState=beforeFullScreenState; Padding=new Padding(WindowState==FormWindowState.Maximized?0:5); }
      };
      core.NewWindowRequested += (s,a) => a.Handled = true;
      core.NavigationStarting += (s,a) => { if (!a.Uri.StartsWith(site, StringComparison.OrdinalIgnoreCase)) a.Cancel = true; };
      core.Navigate(site + "index.html");
    } catch (Exception ex) { MessageBox.Show("无法启动 WebView2。请确认已安装 WebView2 Runtime。\n" + ex.Message,"FDP",MessageBoxButtons.OK,MessageBoxIcon.Error); Close(); }
  }
  void Resource(object sender, CoreWebView2WebResourceRequestedEventArgs e) {
    try {
      if(e.Request.Uri.StartsWith("https://fdp-font.local/",StringComparison.Ordinal)) {string kind=e.Request.Uri.Substring("https://fdp-font.local/".Length),file=kind=="yahei"?"msyh.ttc":kind=="calibri"?"calibri.ttf":null;if(file==null){e.Response=view.CoreWebView2.Environment.CreateWebResourceResponse(null,404,"Not Found","");return;}string fontPath=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts),file);if(!File.Exists(fontPath)){e.Response=view.CoreWebView2.Environment.CreateWebResourceResponse(null,404,"Not Found","");return;}var fontStream=new FileStream(fontPath,FileMode.Open,FileAccess.Read,FileShare.Read);e.Response=view.CoreWebView2.Environment.CreateWebResourceResponse(fontStream,200,"OK","Content-Type: application/octet-stream\r\nAccess-Control-Allow-Origin: https://fdp.local\r\nCache-Control: no-store\r\n");return;}
      string token=e.Request.Uri.StartsWith(documentSite,StringComparison.Ordinal) ? e.Request.Uri.Substring(documentSite.Length) : "", path;
      if (!documents.TryGetValue(token,out path)) { e.Response = view.CoreWebView2.Environment.CreateWebResourceResponse(null,404,"Not Found",""); return; }
      var stream = new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read | FileShare.Delete);
      e.Response = view.CoreWebView2.Environment.CreateWebResourceResponse(stream,200,"OK","Content-Type: application/pdf\r\nAccess-Control-Allow-Origin: https://fdp.local\r\nCache-Control: no-store\r\n");
    } catch { e.Response = view.CoreWebView2.Environment.CreateWebResourceResponse(null,500,"Read error",""); }
  }
  void Send(object o) { view.CoreWebView2.PostWebMessageAsJson(json.Serialize(o)); }
  void CancelTransfer() { if(transfer==null)return;var pending=transfer;transfer=null;if(pending.stream!=null)pending.stream.Dispose();try{if(File.Exists(pending.temp))File.Delete(pending.temp);}catch{} }
  void Message(object sender, CoreWebView2WebMessageReceivedEventArgs e) {
    if (!e.Source.StartsWith(site,StringComparison.OrdinalIgnoreCase)) return;
    string action=null, requestId=null;
    try {
      var m = json.DeserializeObject(e.WebMessageAsJson) as System.Collections.Generic.Dictionary<string,object>;
      if (m == null || !m.ContainsKey("action")) return;
      action = m["action"] as string; object requestValue;if(m.TryGetValue("id",out requestValue))requestId=requestValue as string;
      if (action == "open") {
        using (var d = new OpenFileDialog { Filter = "PDF 文件|*.pdf", CheckFileExists = true }) if (d.ShowDialog(this) == DialogResult.OK) {
          ChooseFile(d.FileName);
        }
      } else if(action=="app-ready") { Send(new {type="default-status",isDefault=IsDefaultPdf()});pageReady=true;string initial=PdfArgument(Environment.GetCommandLineArgs().Skip(1).ToArray());if(initial!=null)ChooseFile(initial);foreach(string path in pendingFiles.ToArray())AcceptExternal(path);pendingFiles.Clear(); }
      else if(action=="default-status") Send(new {type="default-status",isDefault=IsDefaultPdf()});
      else if(action=="default-settings") {RegisterPdf();try{Process.Start(new ProcessStartInfo("ms-settings:defaultapps?registeredAppUser="+Uri.EscapeDataString("FDP 极简版")){UseShellExecute=true});}catch{Process.Start(new ProcessStartInfo("ms-settings:defaultapps"){UseShellExecute=true});}Send(new {type="default-settings-opened"});}
      else if (action == "recent-list") SendRecent();
      else if (action == "recent-record") {
        object value; string path;if(!m.TryGetValue("token",out value)||!documents.TryGetValue(value as string ?? "",out path))return;
        var items=ReadRecent(); var existing=items.FirstOrDefault(x=>String.Equals(x.path,path,StringComparison.OrdinalIgnoreCase));
        items.RemoveAll(x=>String.Equals(x.path,path,StringComparison.OrdinalIgnoreCase));items.Insert(0,new Recent{id=existing==null?Guid.NewGuid().ToString():existing.id,path=path});WriteRecent(items.Take(10).ToList());
      } else if(action=="recent-open") {
        object value; if(!m.TryGetValue("id",out value))return;var item=ReadRecent().FirstOrDefault(x=>x.id==(value as string));if(item==null)throw new IOException("最近文件记录不存在");if(!File.Exists(item.path))throw new IOException("文件已被移动或删除，请重新选择");ChooseFile(item.path);
      } else if(action=="recent-remove") {
        object value;if(!m.TryGetValue("id",out value))return;var items=ReadRecent();items.RemoveAll(x=>x.id==(value as string));WriteRecent(items);SendRecent();
      } else if(action=="recent-clear") { WriteRecent(new List<Recent>()); SendRecent(); }
      else if(action=="close-document") { object value;if(m.TryGetValue("token",out value)){documents.Remove(value as string ?? "");documentHashes.Remove(value as string ?? "");}SendRecent(); }
      else if(action=="save-begin") {
        if(transfer!=null)throw new IOException("已有保存任务正在进行"); object v;string token,id,path;
        string kind=m.TryGetValue("kind",out v) ? v as string ?? "" : ""; token=""; path="图片合并.pdf";
        if(kind!="images-pdf" && (!m.TryGetValue("token",out v)||!documents.TryGetValue(token=v as string ?? "",out path)))throw new IOException("文档已关闭");
        if(!m.TryGetValue("id",out v)||!ValidId(id=v as string))throw new IOException("保存任务标识无效");
        if(!m.TryGetValue("size",out v))throw new IOException("保存大小无效");long size=Convert.ToInt64(v);if(size<1||size>256L*1024*1024)throw new IOException("文件超过 256 MiB 保存限制");
        int pageNumber=0;if(kind=="png" && (!m.TryGetValue("page",out v)||!Int32.TryParse(Convert.ToString(v),out pageNumber)||pageNumber<1||pageNumber>1000000))throw new IOException("页码无效");
        int part=0;if(kind=="split" && (!m.TryGetValue("part",out v)||!Int32.TryParse(Convert.ToString(v),out part)||part<1||part>100))throw new IOException("拆分序号无效");
        if(kind=="in-place") {
          string originalHash;if(!documentHashes.TryGetValue(token,out originalHash)||!File.Exists(path)||FileHash(path)!=originalHash)throw new IOException("原文件已被其他程序修改或移动，请另存副本后重新打开");
          string dir=Path.GetDirectoryName(path),temp=Path.Combine(dir,".fdp-"+Guid.NewGuid().ToString("N")+".tmp");
          transfer=new SaveTransfer{id=id,token=token,target=path,temp=temp,expected=size,replace=true,stream=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)};
          Send(new {type="save-ready",id=id});return;
        }
        string suffix=kind=="extract" ? "-提取页面.pdf" : kind=="png" ? "-第"+pageNumber+"页.png" : kind=="images-pdf" ? ".pdf" : kind=="split" ? "-拆分"+part+".pdf" : kind=="compress" ? "-压缩.pdf" : "-编辑副本.pdf";
        string filter=kind=="png" ? "PNG 图片|*.png" : "PDF 文件|*.pdf";
        using(var d=new SaveFileDialog {Filter=filter,FileName=Path.GetFileNameWithoutExtension(path)+suffix,OverwritePrompt=false}) {
          if(d.ShowDialog(this)!=DialogResult.OK){Send(new {type="save-canceled",id=id});return;}
          if(File.Exists(d.FileName)||Directory.Exists(d.FileName))throw new IOException("目标文件已存在，请使用新文件名");
          string dir=Path.GetDirectoryName(d.FileName),temp=Path.Combine(dir,".fdp-"+Guid.NewGuid().ToString("N")+".tmp");
          transfer=new SaveTransfer{id=id,token=token,target=d.FileName,temp=temp,expected=size,stream=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)};
          Send(new {type="save-ready",id=id});
        }
      }
      else if(action=="save-chunk") {
        object v; if(transfer==null||!m.TryGetValue("id",out v)||(v as string)!=transfer.id)throw new IOException("保存任务不存在");
        if(!m.TryGetValue("data",out v)||!(v is string)||((string)v).Length>350000)throw new IOException("保存数据无效");
        byte[] bytes=Convert.FromBase64String((string)v);if(bytes.Length==0||bytes.Length>256*1024||transfer.written+bytes.Length>transfer.expected)throw new IOException("保存数据长度无效");
        transfer.stream.Write(bytes,0,bytes.Length);transfer.written+=bytes.Length;Send(new {type="save-ack",id=transfer.id,written=transfer.written});
      }
      else if(action=="save-abort") {
        object v;if(transfer==null||!m.TryGetValue("id",out v)||(v as string)!=transfer.id)throw new IOException("保存任务不存在");
        string id=transfer.id;CancelTransfer();Send(new {type="save-failed",id=id,message="保存数据进度不一致"});
      }
      else if(action=="save-finish") {
        object v;if(transfer==null||!m.TryGetValue("id",out v)||(v as string)!=transfer.id||transfer.written!=transfer.expected)throw new IOException("保存数据不完整");
        var done=transfer;done.stream.Flush(true);done.stream.Dispose();done.stream=null;if(done.replace) { string originalHash;if(!documentHashes.TryGetValue(done.token,out originalHash)||!File.Exists(done.target)||FileHash(done.target)!=originalHash)throw new IOException("原文件已被其他程序修改或移动，请另存副本后重新打开");File.Replace(done.temp,done.target,null);documentHashes[done.token]=FileHash(done.target); } else File.Move(done.temp,done.target);transfer=null;Send(new {type="save-complete",id=done.id,token=done.token});
      }
      else if (action == "save") {
        object value;string selectedPath;if(!m.TryGetValue("token",out value)||!documents.TryGetValue(value as string ?? "",out selectedPath))throw new IOException("文档已关闭，请重新打开");
        using (var d = new SaveFileDialog { Filter="PDF 文件|*.pdf", FileName=Path.GetFileNameWithoutExtension(selectedPath)+"-副本.pdf", OverwritePrompt=false }) if (d.ShowDialog(this)==DialogResult.OK) {
          // CreateNew refuses existing paths, including the source and hard-link aliases.
          bool created = false;
          try { using (var input = new FileStream(selectedPath,FileMode.Open,FileAccess.Read,FileShare.Read))
          using (var output = new FileStream(d.FileName,FileMode.CreateNew,FileAccess.Write,FileShare.None)) { created = true; input.CopyTo(output); } }
          catch { if (created) try { File.Delete(d.FileName); } catch {} throw; }
          Send(new {type="saved"});
        }
      } else if (action == "print") view.CoreWebView2.ShowPrintUI(CoreWebView2PrintDialogKind.Browser);
      else if (action == "window-drag") { ReleaseCapture(); SendMessage(Handle,WM_NCLBUTTONDOWN,(IntPtr)HTCAPTION,IntPtr.Zero); }
      else if (action == "window-minimize") WindowState=FormWindowState.Minimized;
      else if (action == "window-maximize") ToggleMaximize();
      else if (action == "window-close") Close();
      else if (action == "window-close-approved") { closingApproved=true;Close(); }
    } catch (Exception ex) { if(transfer!=null){string id=transfer.id;CancelTransfer();Send(new {type="save-failed",id=id,message=ex.Message});} else if(action=="save-begin")Send(new {type="save-failed",id=requestId,message=ex.Message});else Send(new { type="error", message=ex.Message }); }
  }
  protected override void OnFormClosing(FormClosingEventArgs e) { if(e.CloseReason==CloseReason.UserClosing && !closingApproved && view.CoreWebView2!=null){e.Cancel=true;Send(new {type="request-close"});return;}base.OnFormClosing(e); }
  protected override void OnFormClosed(FormClosedEventArgs e) { CancelTransfer();base.OnFormClosed(e); }
  [STAThread] static void Main() { bool first;using(var mutex=new Mutex(true,InstanceName,out first)) { if(!first) { try { using(var pipe=new NamedPipeClientStream(".",InstanceName,PipeDirection.Out)) { pipe.Connect(10000);using(var writer=new StreamWriter(pipe,Encoding.UTF8)){writer.WriteLine(PdfArgument(Environment.GetCommandLineArgs().Skip(1).ToArray())??"");} } } catch(Exception ex) { MessageBox.Show("无法连接已打开的 FDP 窗口，请重试。\n"+ex.Message,"FDP",MessageBoxButtons.OK,MessageBoxIcon.Error); } return; } Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false); Application.Run(new FdpLite()); } }
}



