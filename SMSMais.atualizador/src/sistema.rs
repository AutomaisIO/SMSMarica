// O que é do Windows: onde as coisas ficam, o registro (início automático por usuário), o log,
// a instância única, os eventos entre processos, o cofre do usuário (DPAPI) e a saída no console.
// Nada aqui pede administrador — tudo vive no perfil do usuário (%LOCALAPPDATA%) e em
// HKEY_CURRENT_USER.

use std::ffi::c_void;
use std::fs;
use std::io::Write;
use std::path::{Path, PathBuf};
use std::ptr::{null, null_mut};
use std::sync::OnceLock;

use windows_sys::Win32::Foundation::{
    CloseHandle, LocalFree, SetHandleInformation, GENERIC_READ, GENERIC_WRITE, HANDLE, HANDLE_FLAG_INHERIT,
    INVALID_HANDLE_VALUE, SYSTEMTIME, WAIT_ABANDONED, WAIT_OBJECT_0,
};
use windows_sys::Win32::Security::Cryptography::{
    CryptProtectData, CryptUnprotectData, CRYPTPROTECT_LOCAL_MACHINE, CRYPTPROTECT_UI_FORBIDDEN, CRYPT_INTEGER_BLOB,
};
use windows_sys::Win32::Storage::FileSystem::{CreateFileW, WriteFile, FILE_SHARE_WRITE, OPEN_EXISTING};
use windows_sys::Win32::System::Console::{
    AttachConsole, GetConsoleMode, GetStdHandle, WriteConsoleW, ATTACH_PARENT_PROCESS, STD_ERROR_HANDLE,
    STD_INPUT_HANDLE, STD_OUTPUT_HANDLE,
};
use windows_sys::Win32::System::Registry::{
    RegDeleteKeyValueW, RegGetValueW, RegSetKeyValueW, HKEY_CURRENT_USER, REG_SZ, RRF_RT_REG_SZ,
};
use windows_sys::Win32::System::SystemInformation::{GetLocalTime, GetTickCount64};
use windows_sys::Win32::System::Threading::{
    CreateEventW, CreateMutexW, GetCurrentProcessId, OpenEventW, ResetEvent, SetEvent, WaitForMultipleObjects,
    WaitForSingleObject, EVENT_MODIFY_STATE,
};
use windows_sys::Win32::UI::Shell::ShellExecuteW;
use windows_sys::Win32::UI::WindowsAndMessaging::{
    MessageBoxW, MB_ICONERROR, MB_ICONINFORMATION, MB_OK, MB_SETFOREGROUND, SW_SHOWNORMAL,
};

pub const NOME_EXE: &str = "smsmais-atualizador.exe";
pub const TITULO: &str = "SMSMais — Atualizador";
const CHAVE_RUN: &str = r"Software\Microsoft\Windows\CurrentVersion\Run";
const LOG_MAX_BYTES: u64 = 512 * 1024;

pub fn wide(s: &str) -> Vec<u16> {
    s.encode_utf16().chain(std::iter::once(0)).collect()
}

// ------------------------------------------------------------------------ locais
pub struct Locais {
    /// Pasta do atualizador (executável instalado, config, estado e log).
    pub base: PathBuf,
    pub exe: PathBuf,
    pub config: PathBuf,
    pub estado: PathBuf,
    pub log: PathBuf,
    /// Onde a extensão fica quando o PC ainda não tem pasta nenhuma.
    pub extensao_padrao: PathBuf,
    /// Distingue uma raiz alternativa (ensaio) da instalação real no mesmo PC: entra no nome do
    /// mutex, dos eventos e do valor do registro. Vazio na instalação normal.
    pub sufixo: String,
}

/// Nome da pasta que o `atualizar-extensao.bat` antigo usava dentro de `C:\SMSMais`. Onde ela
/// existe, o Chrome já carrega a extensão de lá — continuar nela evita o "Carregar sem
/// compactação" de novo em cada PC.
const PASTA_LEGADA: &str = "extensao-sisreg";

/// Dá para criar `raiz\atualizador` e escrever lá? Só sonda: o que foi criado para a prova é
/// desfeito (um `--status` num PC sem instalação não pode deixar pasta para trás).
fn consegue_escrever(raiz: &Path) -> bool {
    let base = raiz.join("atualizador");
    let (havia_raiz, havia_base) = (raiz.is_dir(), base.is_dir());
    let sonda = base.join(format!(".sonda-{}", std::process::id()));
    let ok = fs::create_dir_all(&base).is_ok() && fs::write(&sonda, b"").is_ok();
    let _ = fs::remove_file(&sonda);
    if !havia_base {
        let _ = fs::remove_dir(&base);
    }
    if !havia_raiz {
        let _ = fs::remove_dir(raiz);
    }
    ok
}

/// A raiz onde tudo mora. O padrão é a pasta COMUM a todos os usuários do PC, `C:\SMSMais`
/// (qualquer usuário cria pasta na raiz do disco e todos conseguem alterá-la — sem administrador):
/// uma instalação, uma autorização e uma extensão por computador. Só se o disco estiver trancado
/// é que cai na pasta pessoal do usuário (`%LOCALAPPDATA%\SMSMais`).
fn raiz() -> PathBuf {
    let disco = std::env::var("SystemDrive").unwrap_or_else(|_| "C:".into());
    let comum = PathBuf::from(format!("{disco}\\SMSMais"));
    let pessoal = std::env::var_os("LOCALAPPDATA").map(PathBuf::from).unwrap_or_else(std::env::temp_dir).join("SMSMais");
    let instalado = |r: &Path| r.join("atualizador").join(NOME_EXE).is_file();
    if instalado(&comum) {
        comum
    } else if instalado(&pessoal) {
        pessoal
    } else if consegue_escrever(&comum) {
        comum
    } else {
        pessoal
    }
}

pub fn locais() -> &'static Locais {
    static L: OnceLock<Locais> = OnceLock::new();
    L.get_or_init(|| {
        // Os testes nunca encostam na instalação de verdade do PC onde rodam.
        let alternativa = if cfg!(test) {
            Some(std::env::temp_dir().join("smsmais-atualizador-testes"))
        } else {
            std::env::var_os("SMSMAIS_RAIZ").filter(|v| !v.is_empty()).map(PathBuf::from)
        };
        let raiz = alternativa.clone().unwrap_or_else(raiz);
        let sufixo = match &alternativa {
            Some(r) => format!("-{:08x}", fnv1a(&r.to_string_lossy().to_lowercase())),
            None => String::new(),
        };
        let legada = raiz.join(PASTA_LEGADA);
        let extensao_padrao = if legada.join("manifest.json").is_file() { legada } else { raiz.join("extensao") };
        let base = raiz.join("atualizador");
        Locais {
            exe: base.join(NOME_EXE),
            config: base.join("config.json"),
            estado: base.join("estado.json"),
            log: base.join("atualizador.log"),
            base,
            extensao_padrao,
            sufixo,
        }
    })
}

fn fnv1a(s: &str) -> u32 {
    s.bytes().fold(0x811c_9dc5u32, |h, b| (h ^ b as u32).wrapping_mul(0x0100_0193))
}

// --------------------------------------------------------------------------- log
fn relogio() -> SYSTEMTIME {
    let mut t: SYSTEMTIME = unsafe { std::mem::zeroed() };
    unsafe { GetLocalTime(&mut t) };
    t
}

/// "21:14" — para a dica do ícone.
pub fn hora() -> String {
    let t = relogio();
    format!("{:02}:{:02}", t.wHour, t.wMinute)
}

/// Escreve uma linha no log (com data). Falha de log nunca derruba o programa.
pub fn registrar(msg: &str) {
    let l = locais();
    let t = relogio();
    let _ = fs::create_dir_all(&l.base);
    if fs::metadata(&l.log).map(|m| m.len() > LOG_MAX_BYTES).unwrap_or(false) {
        let _ = fs::rename(&l.log, l.log.with_extension("log.1"));
    }
    if let Ok(mut f) = fs::OpenOptions::new().create(true).append(true).open(&l.log) {
        let _ = writeln!(
            f,
            "{:04}-{:02}-{:02} {:02}:{:02}:{:02} [{}] {}",
            t.wYear,
            t.wMonth,
            t.wDay,
            t.wHour,
            t.wMinute,
            t.wSecond,
            env!("CARGO_PKG_VERSION"),
            msg
        );
    }
}

// ----------------------------------------------------------------------- console
// O executável é "de janela" (não pisca um console preto a cada logon). Para os comandos de linha
// de comando responderem, a saída vai para o stdout quando ele existe (redirecionado) ou para o
// console de quem chamou.
fn saida() -> HANDLE {
    static H: OnceLock<usize> = OnceLock::new();
    *H.get_or_init(|| unsafe {
        let h = GetStdHandle(STD_OUTPUT_HANDLE);
        if !h.is_null() && h != INVALID_HANDLE_VALUE {
            return h as usize;
        }
        if AttachConsole(ATTACH_PARENT_PROCESS) == 0 {
            return 0;
        }
        let c = CreateFileW(
            wide("CONOUT$").as_ptr(),
            GENERIC_READ | GENERIC_WRITE,
            FILE_SHARE_WRITE,
            null(),
            OPEN_EXISTING,
            0,
            null_mut(),
        );
        if c == INVALID_HANDLE_VALUE {
            0
        } else {
            c as usize
        }
    }) as HANDLE
}

/// Só no console (sem log).
pub fn imprimir(msg: &str) {
    let h = saida();
    if h.is_null() {
        return;
    }
    unsafe {
        let mut modo = 0u32;
        let mut escritos = 0u32;
        if GetConsoleMode(h, &mut modo) != 0 {
            let w: Vec<u16> = format!("{msg}\r\n").encode_utf16().collect();
            WriteConsoleW(h, w.as_ptr(), w.len() as u32, &mut escritos, null());
        } else {
            let b = format!("{msg}\r\n").into_bytes();
            WriteFile(h, b.as_ptr(), b.len() as u32, &mut escritos, null_mut());
        }
    }
}

/// Marca a entrada e as saídas deste processo como não herdáveis pelos processos que ele criar.
pub fn nao_herdar_saida() {
    for qual in [STD_INPUT_HANDLE, STD_OUTPUT_HANDLE, STD_ERROR_HANDLE] {
        unsafe {
            let h = GetStdHandle(qual);
            if !h.is_null() && h != INVALID_HANDLE_VALUE {
                SetHandleInformation(h, HANDLE_FLAG_INHERIT, 0);
            }
        }
    }
}

/// No console e no log.
pub fn dizer(msg: &str) {
    registrar(msg);
    imprimir(msg);
}

pub fn caixa(texto: &str, erro: bool) {
    let icone = if erro { MB_ICONERROR } else { MB_ICONINFORMATION };
    unsafe {
        MessageBoxW(null_mut(), wide(texto).as_ptr(), wide(TITULO).as_ptr(), MB_OK | MB_SETFOREGROUND | icone);
    }
}

/// Abre uma pasta, um arquivo ou um endereço com o programa padrão do usuário.
pub fn abrir(alvo: &str) {
    unsafe {
        ShellExecuteW(null_mut(), wide("open").as_ptr(), wide(alvo).as_ptr(), null(), null(), SW_SHOWNORMAL);
    }
}

pub fn nome_do_computador() -> String {
    std::env::var("COMPUTERNAME").unwrap_or_default()
}

// ---------------------------------------------------- início automático (por usuário)
// HKCU\...\Run: o Windows executa a cada logon DESTE usuário. Não exige administrador.
fn valor_run() -> String {
    format!("SMSMaisAtualizador{}", locais().sufixo)
}

pub fn registrar_inicio_automatico(exe: &Path) -> Result<(), String> {
    let comando = wide(&format!("\"{}\" --residente", exe.display()));
    let r = unsafe {
        RegSetKeyValueW(
            HKEY_CURRENT_USER,
            wide(CHAVE_RUN).as_ptr(),
            wide(&valor_run()).as_ptr(),
            REG_SZ,
            comando.as_ptr() as *const c_void,
            (comando.len() * 2) as u32,
        )
    };
    if r == 0 {
        Ok(())
    } else {
        Err(format!("não consegui gravar o início automático no registro (erro {r})"))
    }
}

pub fn remover_inicio_automatico() {
    unsafe {
        RegDeleteKeyValueW(HKEY_CURRENT_USER, wide(CHAVE_RUN).as_ptr(), wide(&valor_run()).as_ptr());
    }
}

pub fn inicio_automatico() -> Option<String> {
    let mut buf = vec![0u16; 1024];
    let mut bytes = (buf.len() * 2) as u32;
    let r = unsafe {
        RegGetValueW(
            HKEY_CURRENT_USER,
            wide(CHAVE_RUN).as_ptr(),
            wide(&valor_run()).as_ptr(),
            RRF_RT_REG_SZ,
            null_mut(),
            buf.as_mut_ptr() as *mut c_void,
            &mut bytes,
        )
    };
    if r != 0 {
        return None;
    }
    let n = (bytes as usize / 2).min(buf.len());
    Some(String::from_utf16_lossy(&buf[..n]).trim_end_matches('\0').to_string())
}

// --------------------------------------------------------- cofre do computador
// DPAPI com escopo de MÁQUINA: o Windows cifra com a chave deste computador. O token fica no
// config.json da pasta comum e vale para qualquer usuário DESTE PC (a autorização é do
// computador); copiado para outro PC, o arquivo não revela nada.
fn dpapi(dados: &[u8], cifrar: bool) -> Option<Vec<u8>> {
    let entrada = CRYPT_INTEGER_BLOB { cbData: dados.len() as u32, pbData: dados.as_ptr() as *mut u8 };
    let mut saida = CRYPT_INTEGER_BLOB { cbData: 0, pbData: null_mut() };
    unsafe {
        let ok = if cifrar {
            let escopo = CRYPTPROTECT_UI_FORBIDDEN | CRYPTPROTECT_LOCAL_MACHINE;
            CryptProtectData(&entrada, null(), null(), null(), null(), escopo, &mut saida)
        } else {
            CryptUnprotectData(&entrada, null_mut(), null(), null(), null(), CRYPTPROTECT_UI_FORBIDDEN, &mut saida)
        };
        if ok == 0 || saida.pbData.is_null() {
            return None;
        }
        let bytes = std::slice::from_raw_parts(saida.pbData, saida.cbData as usize).to_vec();
        LocalFree(saida.pbData as *mut c_void);
        Some(bytes)
    }
}

/// Cifra um segredo para este computador; devolve em hexadecimal (cabe num JSON).
pub fn proteger(segredo: &str) -> Option<String> {
    dpapi(segredo.as_bytes(), true).map(|b| b.iter().map(|x| format!("{x:02x}")).collect())
}

pub fn revelar(hex: &str) -> Option<String> {
    if hex.is_empty() || hex.len() % 2 != 0 || !hex.is_ascii() {
        return None;
    }
    let bytes: Option<Vec<u8>> = (0..hex.len()).step_by(2).map(|i| u8::from_str_radix(&hex[i..i + 2], 16).ok()).collect();
    String::from_utf8(dpapi(&bytes?, false)?).ok()
}

// ------------------------------------------------------- instância única e eventos
fn nome(objeto: &str) -> Vec<u16> {
    wide(&format!("Local\\SMSMaisAtualizador{objeto}{}", locais().sufixo))
}

/// Posse da instância única. Vale enquanto o processo viver (o Windows libera o mutex na saída).
pub struct Instancia(#[allow(dead_code)] HANDLE);

/// Espera até `espera_ms` pela vez — o residente novo nasce enquanto o antigo ainda está saindo
/// (auto-atualização, reinstalação). `None` = já existe outro residente vivo.
pub fn instancia_unica(espera_ms: u32) -> Option<Instancia> {
    unsafe {
        let h = CreateMutexW(null(), 0, nome("").as_ptr());
        if h.is_null() {
            return None;
        }
        match WaitForSingleObject(h, espera_ms) {
            WAIT_OBJECT_0 | WAIT_ABANDONED => Some(Instancia(h)),
            _ => {
                CloseHandle(h);
                None
            }
        }
    }
}

pub fn residente_vivo() -> bool {
    match instancia_unica(0) {
        Some(i) => {
            unsafe { CloseHandle(i.0) };
            false
        }
        None => true,
    }
}

#[derive(Clone, Copy)]
pub enum Sinal {
    /// Encerrar o residente (desinstalar, reinstalar, "Sair" do menu).
    Parar,
    /// Conferir já, sem esperar o intervalo ("Verificar agora", fim do Configurar).
    Agora,
}

impl Sinal {
    fn nome(self) -> Vec<u16> {
        nome(match self {
            Sinal::Parar => "Parar",
            Sinal::Agora => "Agora",
        })
    }
}

/// Avisa o residente (de qualquer processo). `true` = havia um residente para avisar.
pub fn sinalizar(sinal: Sinal) -> bool {
    unsafe {
        let h = OpenEventW(EVENT_MODIFY_STATE, 0, sinal.nome().as_ptr());
        if h.is_null() {
            return false;
        }
        SetEvent(h);
        CloseHandle(h);
        true
    }
}

pub enum Acordou {
    Parar,
    Agora,
    Tempo,
}

/// Os dois eventos em que o residente dorme: acorda no fim do intervalo, quando pedem para
/// conferir agora, ou quando mandam parar.
pub struct Eventos {
    parar: HANDLE,
    agora: HANDLE,
}

// As alças de evento do Windows podem ser usadas de qualquer thread.
unsafe impl Send for Eventos {}
unsafe impl Sync for Eventos {}

impl Eventos {
    /// Chamar só depois de ter a instância única: um pedido de parada feito ao residente anterior
    /// (evento ainda sinalizado, se ele demorou a sair) não pode derrubar o que acabou de nascer.
    pub fn criar() -> Option<Eventos> {
        unsafe {
            let parar = CreateEventW(null(), 1, 0, Sinal::Parar.nome().as_ptr());
            let agora = CreateEventW(null(), 0, 0, Sinal::Agora.nome().as_ptr());
            if parar.is_null() || agora.is_null() {
                return None;
            }
            ResetEvent(parar);
            ResetEvent(agora);
            Some(Eventos { parar, agora })
        }
    }

    pub fn esperar(&self, ms: u32) -> Acordou {
        let alcas = [self.parar, self.agora];
        match unsafe { WaitForMultipleObjects(2, alcas.as_ptr(), 0, ms) } {
            r if r == WAIT_OBJECT_0 => Acordou::Parar,
            r if r == WAIT_OBJECT_0 + 1 => Acordou::Agora,
            _ => Acordou::Tempo,
        }
    }
}

// ------------------------------------------------------------------------ miúdos
pub fn milissegundos_ligado() -> u64 {
    unsafe { GetTickCount64() }
}

/// Espalha as consultas dos PCs (todos atrás do mesmo IP da prefeitura) para não baterem juntos.
pub fn sorteio(ate: u64) -> u64 {
    if ate == 0 {
        return 0;
    }
    let pid = unsafe { GetCurrentProcessId() } as u64;
    (milissegundos_ligado() ^ pid.wrapping_mul(0x9e37_79b9_7f4a_7c15)) % ate
}

pub fn mesmo_arquivo(a: &Path, b: &Path) -> bool {
    match (fs::canonicalize(a), fs::canonicalize(b)) {
        (Ok(x), Ok(y)) => x.to_string_lossy().eq_ignore_ascii_case(&y.to_string_lossy()),
        _ => false,
    }
}

#[cfg(test)]
mod testes {
    use super::*;

    #[test]
    fn cofre_vai_e_volta_e_nao_guarda_em_claro() {
        let segredo = "token-do-dispositivo-çã-123";
        let cifrado = proteger(segredo).expect("DPAPI disponível");
        assert!(!cifrado.contains("token"), "o que vai para o arquivo não pode conter o segredo");
        assert_eq!(revelar(&cifrado).as_deref(), Some(segredo));
    }

    #[test]
    fn cofre_recusa_lixo() {
        assert_eq!(revelar(""), None);
        assert_eq!(revelar("zz"), None);
        assert_eq!(revelar("abc"), None);
        assert_eq!(revelar("00112233"), None, "bytes que o Windows não cifrou não abrem");
    }
}
