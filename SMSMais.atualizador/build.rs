// Embute no executável: o ícone padrão, as informações de versão e o manifesto de aplicação do
// Windows. O manifesto declara `asInvoker` — o executável NUNCA pede elevação (sem isso o Windows
// pode deduzir pelo nome que é um instalador e abrir o UAC).

const MANIFESTO: &str = r#"<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<assembly xmlns="urn:schemas-microsoft-com:asm.v1" xmlns:asmv3="urn:schemas-microsoft-com:asm.v3" manifestVersion="1.0">
  <assemblyIdentity type="win32" name="SMSMais.Atualizador" version="1.0.0.0"/>
  <trustInfo xmlns="urn:schemas-microsoft-com:asm.v3">
    <security>
      <requestedPrivileges>
        <requestedExecutionLevel level="asInvoker" uiAccess="false"/>
      </requestedPrivileges>
    </security>
  </trustInfo>
  <compatibility xmlns="urn:schemas-microsoft-com:compatibility.v1">
    <application>
      <supportedOS Id="{8e0f7a12-bfb3-4fe8-b9a5-48fd50a15a9a}"/>
    </application>
  </compatibility>
  <asmv3:application>
    <asmv3:windowsSettings>
      <dpiAware xmlns="http://schemas.microsoft.com/SMI/2005/WindowsSettings">true/pm</dpiAware>
      <dpiAwareness xmlns="http://schemas.microsoft.com/SMI/2016/WindowsSettings">PerMonitorV2</dpiAwareness>
      <longPathAware xmlns="http://schemas.microsoft.com/SMI/2016/WindowsSettings">true</longPathAware>
    </asmv3:windowsSettings>
  </asmv3:application>
  <dependency>
    <dependentAssembly>
      <assemblyIdentity type="win32" name="Microsoft.Windows.Common-Controls" version="6.0.0.0" processorArchitecture="*" publicKeyToken="6595b64144ccf1df" language="*"/>
    </dependentAssembly>
  </dependency>
</assembly>
"#;

fn main() {
    if std::env::var_os("CARGO_CFG_WINDOWS").is_some() {
        let mut recursos = winresource::WindowsResource::new();
        recursos.set_icon("recursos/icone.ico");
        recursos.set_manifest(MANIFESTO);
        recursos.set("FileDescription", "SMSMais — Atualizador");
        recursos.set("ProductName", "SMSMais");
        recursos.set_language(0x0416); // pt-BR
        recursos.compile().expect("não consegui embutir os recursos do Windows");
    }
    println!("cargo:rerun-if-changed=build.rs");
    println!("cargo:rerun-if-changed=recursos/icone.ico");
}
