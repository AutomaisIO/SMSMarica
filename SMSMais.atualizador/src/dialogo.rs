// Uma janelinha de pergunta com um campo de texto (o "Configurar" pede o endereço da plataforma).
// Win32 puro: rótulo, campo, OK e Cancelar. Roda o próprio laço de mensagens até fechar — quem
// chama precisa estar numa thread só dela.

use std::ptr::{null, null_mut};
use std::sync::Mutex;

use windows_sys::Win32::Foundation::{HWND, LPARAM, LRESULT, WPARAM};
use windows_sys::Win32::Graphics::Gdi::{CreateFontIndirectW, DeleteObject, COLOR_BTNFACE, HBRUSH};
use windows_sys::Win32::System::LibraryLoader::GetModuleHandleW;
use windows_sys::Win32::UI::HiDpi::GetDpiForSystem;
use windows_sys::Win32::UI::Input::KeyboardAndMouse::SetFocus;
use windows_sys::Win32::UI::WindowsAndMessaging::{
    CreateWindowExW, DefWindowProcW, DestroyWindow, DispatchMessageW, GetDlgItem, GetMessageW, GetSystemMetrics,
    GetWindowTextLengthW, GetWindowTextW, IsDialogMessageW, LoadCursorW, LoadImageW, PostQuitMessage, RegisterClassW,
    SendMessageW, SetForegroundWindow, ShowWindow, SystemParametersInfoW, TranslateMessage, BS_DEFPUSHBUTTON,
    ES_AUTOHSCROLL, IDC_ARROW, IMAGE_ICON, LR_DEFAULTCOLOR, LR_SHARED, MSG, NONCLIENTMETRICSW, SM_CXSCREEN,
    SM_CYSCREEN, SPI_GETNONCLIENTMETRICS, SW_SHOW, WM_CLOSE, WM_COMMAND, WM_DESTROY, WM_SETFONT, WNDCLASSW,
    WS_CAPTION, WS_CHILD, WS_EX_CLIENTEDGE, WS_EX_DLGMODALFRAME, WS_EX_TOPMOST, WS_POPUP, WS_SYSMENU, WS_TABSTOP,
    WS_VISIBLE,
};

use crate::sistema::wide;

const ID_OK: isize = 1; // o Enter do teclado chega como este id
const ID_CANCELAR: isize = 2; // e o Esc, como este
const ID_CAMPO: isize = 100;
const EM_SETSEL: u32 = 0x00B1;

/// `Some(resposta)` no OK, `None` no cancelar. Só há uma pergunta aberta por vez.
static RESPOSTA: Mutex<Option<String>> = Mutex::new(None);

unsafe extern "system" fn janela(hwnd: HWND, msg: u32, w: WPARAM, l: LPARAM) -> LRESULT {
    match msg {
        WM_COMMAND => {
            match (w & 0xffff) as isize {
                ID_OK => {
                    let campo = GetDlgItem(hwnd, ID_CAMPO as i32);
                    let mut buf = vec![0u16; GetWindowTextLengthW(campo) as usize + 1];
                    let n = GetWindowTextW(campo, buf.as_mut_ptr(), buf.len() as i32) as usize;
                    *RESPOSTA.lock().unwrap_or_else(|e| e.into_inner()) = Some(String::from_utf16_lossy(&buf[..n]));
                    DestroyWindow(hwnd);
                }
                ID_CANCELAR => {
                    DestroyWindow(hwnd);
                }
                _ => {}
            }
            0
        }
        WM_CLOSE => {
            DestroyWindow(hwnd);
            0
        }
        WM_DESTROY => {
            PostQuitMessage(0);
            0
        }
        _ => DefWindowProcW(hwnd, msg, w, l),
    }
}

pub fn perguntar(titulo: &str, rotulo: &str, inicial: &str) -> Option<String> {
    *RESPOSTA.lock().unwrap_or_else(|e| e.into_inner()) = None;
    unsafe {
        let instancia = GetModuleHandleW(null());
        let classe = wide("SMSMaisAtualizadorPergunta");
        let mut wc: WNDCLASSW = std::mem::zeroed();
        wc.lpfnWndProc = Some(janela);
        wc.hInstance = instancia;
        wc.lpszClassName = classe.as_ptr();
        wc.hCursor = LoadCursorW(null_mut(), IDC_ARROW);
        wc.hbrBackground = (COLOR_BTNFACE + 1) as HBRUSH;
        wc.hIcon = LoadImageW(instancia, 1 as *const u16, IMAGE_ICON, 0, 0, LR_DEFAULTCOLOR | LR_SHARED) as _;
        RegisterClassW(&wc); // na segunda vez a classe já existe; tudo bem

        // O executável é ciente de DPI: as medidas (pensadas a 96 dpi) são escaladas aqui.
        let dpi = GetDpiForSystem() as i32;
        let e = |v: i32| v * dpi / 96;
        let (largura, altura) = (e(470), e(190));
        let hwnd = CreateWindowExW(
            WS_EX_DLGMODALFRAME | WS_EX_TOPMOST,
            classe.as_ptr(),
            wide(titulo).as_ptr(),
            WS_POPUP | WS_CAPTION | WS_SYSMENU,
            (GetSystemMetrics(SM_CXSCREEN) - largura) / 2,
            (GetSystemMetrics(SM_CYSCREEN) - altura) / 2,
            largura,
            altura,
            null_mut(),
            null_mut(),
            instancia,
            null(),
        );
        if hwnd.is_null() {
            return None;
        }

        // A fonte das caixas de mensagem do Windows (Segoe UI), não a fonte de sistema antiga.
        let mut metricas: NONCLIENTMETRICSW = std::mem::zeroed();
        metricas.cbSize = std::mem::size_of::<NONCLIENTMETRICSW>() as u32;
        SystemParametersInfoW(SPI_GETNONCLIENTMETRICS, metricas.cbSize, &mut metricas as *mut _ as *mut _, 0);
        let fonte = CreateFontIndirectW(&metricas.lfMessageFont);

        let filho = |classe: &str, texto: &str, estilo: u32, ex: u32, x: i32, y: i32, larg: i32, alt: i32, id: isize| {
            let h = CreateWindowExW(
                ex,
                wide(classe).as_ptr(),
                wide(texto).as_ptr(),
                WS_CHILD | WS_VISIBLE | estilo,
                e(x),
                e(y),
                e(larg),
                e(alt),
                hwnd,
                id as _,
                instancia,
                null(),
            );
            SendMessageW(h, WM_SETFONT, fonte as WPARAM, 1);
            h
        };
        filho("STATIC", rotulo, 0, 0, 16, 14, 425, 54, -1);
        let campo = filho("EDIT", inicial, WS_TABSTOP | ES_AUTOHSCROLL as u32, WS_EX_CLIENTEDGE, 16, 72, 425, 24, ID_CAMPO);
        filho("BUTTON", "OK", WS_TABSTOP | BS_DEFPUSHBUTTON as u32, 0, 255, 110, 88, 28, ID_OK);
        filho("BUTTON", "Cancelar", WS_TABSTOP, 0, 353, 110, 88, 28, ID_CANCELAR);

        SendMessageW(campo, EM_SETSEL, 0, -1);
        ShowWindow(hwnd, SW_SHOW);
        SetForegroundWindow(hwnd);
        SetFocus(campo);

        let mut msg: MSG = std::mem::zeroed();
        while GetMessageW(&mut msg, null_mut(), 0, 0) > 0 {
            // IsDialogMessage dá o Tab entre os campos, o Enter (OK) e o Esc (Cancelar).
            if IsDialogMessageW(hwnd, &msg) == 0 {
                TranslateMessage(&msg);
                DispatchMessageW(&msg);
            }
        }
        DeleteObject(fonte as _);
    }
    RESPOSTA.lock().unwrap_or_else(|e| e.into_inner()).take().map(|s| s.trim().to_string()).filter(|s| !s.is_empty())
}
