// O ícone na bandeja do Windows (área de notificação): mostra que o atualizador está vivo, em que
// versão a extensão está, e dá o menu — verificar agora, abrir a pasta, o registro, configurar e
// sair. Também é por aqui que sai o aviso quando a extensão é atualizada.
//
// Quem trabalha é a thread dos ciclos (main.rs); esta aqui só cuida da janela escondida que o
// Windows exige para o ícone. As duas se falam por `situacao`/`avisar`/`encerrar`.

use std::ptr::{null, null_mut};
use std::sync::atomic::{AtomicBool, AtomicIsize, AtomicU32, Ordering};
use std::sync::Mutex;

use windows_sys::Win32::Foundation::{HWND, LPARAM, LRESULT, POINT, WPARAM};
use windows_sys::Win32::System::LibraryLoader::GetModuleHandleW;
use windows_sys::Win32::UI::Shell::{
    Shell_NotifyIconW, NIF_ICON, NIF_INFO, NIF_MESSAGE, NIF_TIP, NIIF_INFO, NIM_ADD, NIM_DELETE, NIM_MODIFY,
    NOTIFYICONDATAW,
};
use windows_sys::Win32::UI::WindowsAndMessaging::{
    AppendMenuW, CreatePopupMenu, CreateWindowExW, DefWindowProcW, DestroyMenu, DestroyWindow, DispatchMessageW,
    GetCursorPos, GetMessageW, GetSystemMetrics, LoadImageW, PostMessageW, PostQuitMessage, RegisterClassW,
    RegisterWindowMessageW, SetForegroundWindow, TrackPopupMenu, TranslateMessage, HICON, IMAGE_ICON,
    LR_DEFAULTCOLOR, MF_GRAYED, MF_SEPARATOR, MF_STRING, MSG, SM_CXSMICON, SM_CYSMICON, TPM_BOTTOMALIGN,
    TPM_NONOTIFY, TPM_RETURNCMD, TPM_RIGHTBUTTON, WM_APP, WM_CLOSE, WM_CONTEXTMENU, WM_DESTROY, WM_LBUTTONUP,
    WM_NULL, WM_RBUTTONUP, WNDCLASSW,
};

use crate::sistema::{wide, TITULO};

const MSG_ICONE: u32 = WM_APP + 1; // o Windows avisa: clicaram no ícone
const MSG_SITUACAO: u32 = WM_APP + 2; // a thread dos ciclos avisa: mudou a dica
const MSG_AVISO: u32 = WM_APP + 3; // a thread dos ciclos avisa: há um aviso a mostrar

const CMD_AGORA: usize = 1;
const CMD_PASTA: usize = 2;
const CMD_REGISTRO: usize = 3;
const CMD_CONFIGURAR: usize = 4;
const CMD_SAIR: usize = 5;

/// O que o menu faz — quem sabe fazer é o main.rs.
pub struct Acoes {
    pub agora: fn(),
    pub pasta: fn(),
    pub registro: fn(),
    pub configurar: fn(),
    pub sair: fn(),
}

struct Painel {
    /// Dica ao passar o mouse (até 127 caracteres).
    dica: String,
    /// Segunda linha do menu: a situação da extensão.
    linha: String,
    aviso: Option<(String, String)>,
    acoes: Option<Acoes>,
}

static PAINEL: Mutex<Painel> =
    Mutex::new(Painel { dica: String::new(), linha: String::new(), aviso: None, acoes: None });
static JANELA: AtomicIsize = AtomicIsize::new(0);
static TASKBAR_CRIADA: AtomicU32 = AtomicU32::new(0);
static FIM: AtomicBool = AtomicBool::new(false);

fn painel() -> std::sync::MutexGuard<'static, Painel> {
    PAINEL.lock().unwrap_or_else(|e| e.into_inner())
}

fn avisar_janela(msg: u32) {
    let h = JANELA.load(Ordering::SeqCst);
    if h != 0 {
        unsafe { PostMessageW(h as HWND, msg, 0, 0) };
    }
}

/// Atualiza a dica do ícone e a linha de situação do menu.
pub fn situacao(dica: &str, linha: &str) {
    {
        let mut p = painel();
        p.dica = dica.to_string();
        p.linha = linha.to_string();
    }
    avisar_janela(MSG_SITUACAO);
}

/// Mostra um aviso do Windows saindo do ícone.
pub fn avisar(titulo: &str, texto: &str) {
    painel().aviso = Some((titulo.to_string(), texto.to_string()));
    avisar_janela(MSG_AVISO);
}

/// Tira o ícone e encerra o laço de mensagens (a thread dos ciclos terminou). Vale mesmo se a
/// janela ainda não existir: `rodar` confere o pedido assim que a cria.
pub fn encerrar() {
    FIM.store(true, Ordering::SeqCst);
    avisar_janela(WM_CLOSE);
}

fn copiar(destino: &mut [u16], texto: &str) {
    let w: Vec<u16> = texto.encode_utf16().take(destino.len() - 1).collect();
    destino[..w.len()].copy_from_slice(&w);
    destino[w.len()] = 0;
}

unsafe fn icone() -> HICON {
    // O ícone embutido no executável (recurso 1), no tamanho que a bandeja pede neste monitor.
    LoadImageW(
        GetModuleHandleW(null()),
        1 as *const u16,
        IMAGE_ICON,
        GetSystemMetrics(SM_CXSMICON),
        GetSystemMetrics(SM_CYSMICON),
        LR_DEFAULTCOLOR,
    ) as HICON
}

unsafe fn dados(hwnd: HWND) -> NOTIFYICONDATAW {
    let mut d: NOTIFYICONDATAW = std::mem::zeroed();
    d.cbSize = std::mem::size_of::<NOTIFYICONDATAW>() as u32;
    d.hWnd = hwnd;
    d.uID = 1;
    d
}

unsafe fn por_icone(hwnd: HWND) -> bool {
    let mut d = dados(hwnd);
    d.uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP;
    d.uCallbackMessage = MSG_ICONE;
    d.hIcon = icone();
    copiar(&mut d.szTip, &painel().dica);
    Shell_NotifyIconW(NIM_ADD, &d) != 0
}

unsafe fn menu(hwnd: HWND) {
    let m = CreatePopupMenu();
    if m.is_null() {
        return;
    }
    let item = |flags, id: usize, texto: &str| {
        AppendMenuW(m, flags, id, wide(texto).as_ptr());
    };
    let linha = painel().linha.clone();
    item(MF_STRING | MF_GRAYED, 0, &format!("{TITULO} v{}", env!("CARGO_PKG_VERSION")));
    if !linha.is_empty() {
        item(MF_STRING | MF_GRAYED, 0, &linha);
    }
    AppendMenuW(m, MF_SEPARATOR, 0, null());
    item(MF_STRING, CMD_AGORA, "Verificar agora");
    item(MF_STRING, CMD_PASTA, "Abrir a pasta da extensão");
    item(MF_STRING, CMD_REGISTRO, "Abrir o registro");
    item(MF_STRING, CMD_CONFIGURAR, "Configurar…");
    AppendMenuW(m, MF_SEPARATOR, 0, null());
    item(MF_STRING, CMD_SAIR, "Sair");

    let mut p = POINT { x: 0, y: 0 };
    GetCursorPos(&mut p);
    // Sem trazer a janela para a frente, o menu não fecha quando se clica fora dele.
    SetForegroundWindow(hwnd);
    let escolha = TrackPopupMenu(m, TPM_RETURNCMD | TPM_NONOTIFY | TPM_RIGHTBUTTON | TPM_BOTTOMALIGN, p.x, p.y, 0, hwnd, null());
    PostMessageW(hwnd, WM_NULL, 0, 0);
    DestroyMenu(m);

    let acao = {
        let p = painel();
        p.acoes.as_ref().and_then(|a| match escolha as usize {
            CMD_AGORA => Some(a.agora),
            CMD_PASTA => Some(a.pasta),
            CMD_REGISTRO => Some(a.registro),
            CMD_CONFIGURAR => Some(a.configurar),
            CMD_SAIR => Some(a.sair),
            _ => None,
        })
    };
    if let Some(f) = acao {
        f();
    }
}

unsafe extern "system" fn janela(hwnd: HWND, msg: u32, w: WPARAM, l: LPARAM) -> LRESULT {
    match msg {
        MSG_ICONE => {
            if matches!(l as u32, WM_RBUTTONUP | WM_LBUTTONUP | WM_CONTEXTMENU) {
                menu(hwnd);
            }
            0
        }
        MSG_SITUACAO => {
            let mut d = dados(hwnd);
            d.uFlags = NIF_TIP;
            copiar(&mut d.szTip, &painel().dica);
            Shell_NotifyIconW(NIM_MODIFY, &d);
            0
        }
        MSG_AVISO => {
            if let Some((titulo, texto)) = painel().aviso.take() {
                let mut d = dados(hwnd);
                d.uFlags = NIF_INFO;
                d.dwInfoFlags = NIIF_INFO;
                copiar(&mut d.szInfoTitle, &titulo);
                copiar(&mut d.szInfo, &texto);
                Shell_NotifyIconW(NIM_MODIFY, &d);
            }
            0
        }
        WM_CLOSE => {
            DestroyWindow(hwnd);
            0
        }
        WM_DESTROY => {
            Shell_NotifyIconW(NIM_DELETE, &dados(hwnd));
            JANELA.store(0, Ordering::SeqCst);
            PostQuitMessage(0);
            0
        }
        // O Explorer reiniciou (ou só agora subiu a barra, logo após o logon): o ícone some e
        // precisa ser posto de novo.
        m if m != 0 && m == TASKBAR_CRIADA.load(Ordering::SeqCst) => {
            por_icone(hwnd);
            0
        }
        _ => DefWindowProcW(hwnd, msg, w, l),
    }
}

/// Põe o ícone e fica no laço de mensagens até `encerrar()`. `false` = não deu para criar a
/// janela (sessão sem área de trabalho); quem chamou segue sem ícone.
pub fn rodar(acoes: Acoes) -> bool {
    painel().acoes = Some(acoes);
    unsafe {
        let instancia = GetModuleHandleW(null());
        let classe = wide("SMSMaisAtualizadorBandeja");
        let mut wc: WNDCLASSW = std::mem::zeroed();
        wc.lpfnWndProc = Some(janela);
        wc.hInstance = instancia;
        wc.lpszClassName = classe.as_ptr();
        RegisterClassW(&wc);
        TASKBAR_CRIADA.store(RegisterWindowMessageW(wide("TaskbarCreated").as_ptr()), Ordering::SeqCst);

        // Janela comum, nunca mostrada (uma janela "só de mensagens" não receberia o aviso de
        // que a barra de tarefas foi recriada).
        let hwnd = CreateWindowExW(0, classe.as_ptr(), wide(TITULO).as_ptr(), 0, 0, 0, 0, 0, null_mut(), null_mut(), instancia, null());
        if hwnd.is_null() {
            return false;
        }
        JANELA.store(hwnd as isize, Ordering::SeqCst);
        // No logon a barra pode ainda não existir; aí o ícone entra pelo aviso "TaskbarCreated".
        por_icone(hwnd);
        if FIM.load(Ordering::SeqCst) {
            PostMessageW(hwnd, WM_CLOSE, 0, 0);
        }

        let mut msg: MSG = std::mem::zeroed();
        while GetMessageW(&mut msg, null_mut(), 0, 0) > 0 {
            TranslateMessage(&msg);
            DispatchMessageW(&msg);
        }
        true
    }
}
