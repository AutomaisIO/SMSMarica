// HTTP por WinINet — a mesma pilha de rede do Windows que o navegador usa: respeita o proxy
// configurado no PC (inclusive script de proxy) e os certificados da máquina, o que importa em
// rede de prefeitura com inspeção de tráfego. E não acrescenta biblioteca de TLS ao executável.

use std::ffi::c_void;
use std::ptr::{null, null_mut};

use windows_sys::Win32::Foundation::GetLastError;
use windows_sys::Win32::Networking::WinInet::{
    HttpOpenRequestW, HttpQueryInfoW, HttpSendRequestW, InternetCloseHandle, InternetConnectW, InternetOpenW,
    InternetReadFile, InternetSetOptionW, HTTP_QUERY_FLAG_NUMBER, HTTP_QUERY_STATUS_CODE, INTERNET_FLAG_NO_AUTH,
    INTERNET_FLAG_NO_AUTO_REDIRECT, INTERNET_FLAG_NO_CACHE_WRITE, INTERNET_FLAG_NO_COOKIES, INTERNET_FLAG_NO_UI,
    INTERNET_FLAG_PRAGMA_NOCACHE, INTERNET_FLAG_RELOAD, INTERNET_FLAG_SECURE, INTERNET_OPEN_TYPE_PRECONFIG,
    INTERNET_OPTION_CONNECT_TIMEOUT, INTERNET_OPTION_RECEIVE_TIMEOUT, INTERNET_OPTION_SEND_TIMEOUT,
    INTERNET_SERVICE_HTTP,
};

use crate::sistema::wide;

const TEMPO_LIMITE_MS: u32 = 30_000;

struct Alca(*mut c_void);
impl Drop for Alca {
    fn drop(&mut self) {
        if !self.0.is_null() {
            unsafe { InternetCloseHandle(self.0) };
        }
    }
}

#[derive(Debug, PartialEq)]
struct Endereco {
    https: bool,
    host: String,
    porta: u16,
    caminho: String,
}

/// Só HTTPS. HTTP puro passa apenas para a própria máquina (ensaio com servidor local) — uma
/// configuração apontando para HTTP na rede deixaria qualquer um no caminho trocar o executável
/// ou ler o token.
fn partir(url: &str) -> Option<Endereco> {
    let (https, resto) = if let Some(r) = url.strip_prefix("https://") {
        (true, r)
    } else if let Some(r) = url.strip_prefix("http://") {
        (false, r)
    } else {
        return None;
    };
    let (autoridade, caminho) = match resto.find('/') {
        Some(i) => (&resto[..i], &resto[i..]),
        None => (resto, "/"),
    };
    if autoridade.is_empty() || autoridade.contains(['@', '?', '#', ' ']) {
        return None;
    }
    let (host, porta) = match autoridade.rsplit_once(':') {
        Some((h, p)) => (h, p.parse::<u16>().ok()?),
        None => (autoridade, if https { 443 } else { 80 }),
    };
    if host.is_empty() || (!https && host != "127.0.0.1" && host != "localhost") {
        return None;
    }
    Some(Endereco { https, host: host.to_string(), porta, caminho: caminho.to_string() })
}

pub fn url_permitida(url: &str) -> bool {
    partir(url).is_some()
}

pub struct Resposta {
    pub status: u32,
    pub corpo: Vec<u8>,
}

/// Um pedido HTTP. Com `token` vai `Authorization: Bearer`; com `json`, é POST. Nos dois casos o
/// redirecionamento fica desligado — o token e o corpo não seguem para outro host.
pub fn pedir(url: &str, token: Option<&str>, json: Option<&str>, limite: usize) -> Result<Resposta, String> {
    let e = partir(url).ok_or_else(|| format!("endereço recusado (só https): {url}"))?;
    if token.is_some_and(|t| t.contains(['\r', '\n'])) {
        return Err("token inválido".into());
    }
    unsafe {
        let agente = wide(concat!("SMSMais-Atualizador/", env!("CARGO_PKG_VERSION")));
        let sessao = Alca(InternetOpenW(agente.as_ptr(), INTERNET_OPEN_TYPE_PRECONFIG, null(), null(), 0));
        if sessao.0.is_null() {
            return Err(format!("rede indisponível (erro {})", GetLastError()));
        }
        for opcao in [INTERNET_OPTION_CONNECT_TIMEOUT, INTERNET_OPTION_RECEIVE_TIMEOUT, INTERNET_OPTION_SEND_TIMEOUT] {
            InternetSetOptionW(sessao.0, opcao, &TEMPO_LIMITE_MS as *const u32 as *const c_void, 4);
        }
        let conexao = Alca(InternetConnectW(
            sessao.0,
            wide(&e.host).as_ptr(),
            e.porta,
            null(),
            null(),
            INTERNET_SERVICE_HTTP,
            0,
            0,
        ));
        if conexao.0.is_null() {
            return Err(format!("sem resposta de {} (erro {})", e.host, GetLastError()));
        }

        // Sempre da rede (nunca do cache do WinINet), sem cookies, sem credenciais e sem janelas.
        let mut flags = INTERNET_FLAG_RELOAD
            | INTERNET_FLAG_NO_CACHE_WRITE
            | INTERNET_FLAG_PRAGMA_NOCACHE
            | INTERNET_FLAG_NO_COOKIES
            | INTERNET_FLAG_NO_AUTH
            | INTERNET_FLAG_NO_UI;
        if e.https {
            flags |= INTERNET_FLAG_SECURE;
        }
        if token.is_some() || json.is_some() {
            flags |= INTERNET_FLAG_NO_AUTO_REDIRECT;
        }
        let metodo = wide(if json.is_some() { "POST" } else { "GET" });
        let pedido = Alca(HttpOpenRequestW(
            conexao.0,
            metodo.as_ptr(),
            wide(&e.caminho).as_ptr(),
            null(),
            null(),
            null(),
            flags,
            0,
        ));
        if pedido.0.is_null() {
            return Err(format!("sem resposta de {} (erro {})", e.host, GetLastError()));
        }

        let mut cabecalhos = String::new();
        if let Some(t) = token {
            cabecalhos.push_str(&format!("Authorization: Bearer {t}\r\n"));
        }
        if json.is_some() {
            cabecalhos.push_str("Content-Type: application/json\r\n");
        }
        let cab = wide(&cabecalhos);
        let corpo_envio = json.map(str::as_bytes).unwrap_or(&[]);
        let enviado = HttpSendRequestW(
            pedido.0,
            if cabecalhos.is_empty() { null() } else { cab.as_ptr() },
            (cab.len() - 1) as u32,
            if corpo_envio.is_empty() { null() } else { corpo_envio.as_ptr() as *const c_void },
            corpo_envio.len() as u32,
        );
        if enviado == 0 {
            return Err(format!("sem resposta de {} (erro {})", e.host, GetLastError()));
        }

        let mut status = 0u32;
        let mut tamanho = 4u32;
        let ok = HttpQueryInfoW(
            pedido.0,
            HTTP_QUERY_STATUS_CODE | HTTP_QUERY_FLAG_NUMBER,
            &mut status as *mut u32 as *mut c_void,
            &mut tamanho,
            null_mut(),
        );
        if ok == 0 {
            return Err(format!("resposta ilegível de {} (erro {})", e.host, GetLastError()));
        }

        let mut corpo = Vec::new();
        let mut bloco = vec![0u8; 64 * 1024];
        loop {
            let mut lidos = 0u32;
            if InternetReadFile(pedido.0, bloco.as_mut_ptr() as *mut c_void, bloco.len() as u32, &mut lidos) == 0 {
                return Err(format!("download interrompido de {} (erro {})", e.host, GetLastError()));
            }
            if lidos == 0 {
                return Ok(Resposta { status, corpo });
            }
            corpo.extend_from_slice(&bloco[..lidos as usize]);
            if corpo.len() > limite {
                return Err(format!("resposta de {} maior que o limite de {limite} bytes", e.host));
            }
        }
    }
}

/// Erro de download que carrega o status HTTP — quem chama precisa distinguir o 401 (token
/// revogado: o PC precisa ser autorizado de novo) de uma falha qualquer de rede.
#[derive(Debug)]
pub struct Falha {
    pub status: Option<u32>,
    pub mensagem: String,
}

impl From<Falha> for String {
    fn from(f: Falha) -> String {
        f.mensagem
    }
}

/// GET que devolve o corpo inteiro; qualquer resposta diferente de 200 é falha.
pub fn baixar(url: &str, limite: usize, token: Option<&str>) -> Result<Vec<u8>, Falha> {
    let r = pedir(url, token, None, limite).map_err(|mensagem| Falha { status: None, mensagem })?;
    if r.status == 200 {
        Ok(r.corpo)
    } else {
        let host = partir(url).map(|e| e.host).unwrap_or_default();
        Err(Falha { status: Some(r.status), mensagem: format!("{host} respondeu HTTP {}", r.status) })
    }
}

#[cfg(test)]
mod testes {
    use super::*;

    #[test]
    fn so_https_ou_a_propria_maquina() {
        assert!(url_permitida("https://raw.githubusercontent.com/x/y/main/atualizador.json"));
        assert!(url_permitida("http://127.0.0.1:8099/x"));
        assert!(url_permitida("http://localhost:8099/x"));
        assert!(!url_permitida("http://exemplo.com/x"));
        assert!(!url_permitida("http://127.0.0.1.exemplo.com/x"));
        assert!(!url_permitida("http://127.0.0.1@exemplo.com/x"));
        assert!(!url_permitida("https://usuario:senha@exemplo.com/x"));
        assert!(!url_permitida("file:///c:/x"));
        assert!(!url_permitida("ftp://exemplo.com/x"));
        assert!(!url_permitida("https://"));
    }

    #[test]
    fn partes_do_endereco() {
        assert_eq!(
            partir("https://api.exemplo.gov.br/extensao/pacote?canal=prod"),
            Some(Endereco {
                https: true,
                host: "api.exemplo.gov.br".into(),
                porta: 443,
                caminho: "/extensao/pacote?canal=prod".into()
            })
        );
        assert_eq!(
            partir("http://127.0.0.1:8765"),
            Some(Endereco { https: false, host: "127.0.0.1".into(), porta: 8765, caminho: "/".into() })
        );
        assert_eq!(partir("https://exemplo.com:abc/x"), None);
    }
}
