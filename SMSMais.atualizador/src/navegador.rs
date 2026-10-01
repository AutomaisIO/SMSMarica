// O Chrome está aberto SEM a nossa extensão? O atualizador não fala com o Chrome; ele só LÊ o que
// o Chrome grava no perfil do usuário:
//   <User Data>\<perfil>\Secure Preferences
//     extensions.ui.developer_mode          o "Modo do desenvolvedor" de chrome://extensions
//     extensions.settings.<id>.path         a pasta de uma extensão carregada sem compactação
//     extensions.settings.<id>.disable_reasons / state     presentes quando ela está desativada
// e olha a lista de processos para saber se há um chrome.exe nesta sessão do Windows.
//
// Com o modo desenvolvedor desligado o Chrome DESATIVA a extensão sem compactação (medido no
// Chrome 154) — por isso esse caso tem aviso próprio.

use std::fs;
use std::path::{Path, PathBuf};

use serde_json::Value;
use windows_sys::Win32::Foundation::{CloseHandle, INVALID_HANDLE_VALUE};
use windows_sys::Win32::System::Diagnostics::ToolHelp::{
    CreateToolhelp32Snapshot, Process32FirstW, Process32NextW, PROCESSENTRY32W, TH32CS_SNAPPROCESS,
};
use windows_sys::Win32::System::RemoteDesktop::ProcessIdToSessionId;
use windows_sys::Win32::System::Threading::GetCurrentProcessId;

#[derive(Debug, PartialEq, Clone, Copy)]
pub enum Situacao {
    /// Carregada da nossa pasta, ativa, com o modo desenvolvedor ligado.
    Carregada,
    /// Nenhum perfil do Chrome tem a extensão carregada da nossa pasta.
    NaoCarregada,
    /// Está no Chrome, mas desativada.
    Desativada,
    /// Está no Chrome, mas o modo desenvolvedor foi desligado (o Chrome a desativa).
    ModoDevDesligado,
}

impl Situacao {
    /// Como esta situação é informada à plataforma (o inventário do painel).
    pub fn rotulo(self) -> &'static str {
        match self {
            Situacao::Carregada => "carregada",
            Situacao::NaoCarregada => "nao-carregada",
            Situacao::Desativada => "desativada",
            Situacao::ModoDevDesligado => "modo-dev-desligado",
        }
    }

    /// Texto curto para o ícone; `None` quando está tudo certo.
    pub fn aviso(self) -> Option<&'static str> {
        match self {
            Situacao::Carregada => None,
            Situacao::NaoCarregada => Some("O Chrome está aberto sem a extensão"),
            Situacao::Desativada => Some("A extensão está desativada no Chrome"),
            Situacao::ModoDevDesligado => Some("O modo desenvolvedor do Chrome está desligado"),
        }
    }
}

fn mesma_pasta(a: &str, b: &str) -> bool {
    let limpar = |s: &str| s.replace('/', "\\").trim_end_matches('\\').to_lowercase();
    limpar(a) == limpar(b)
}

fn desativada(entrada: &Value) -> bool {
    // Chrome novo: lista de motivos. Antigo: máscara de bits e/ou `state` (0 = desativada).
    let motivos = match entrada.get("disable_reasons") {
        Some(Value::Array(a)) => !a.is_empty(),
        Some(Value::Number(n)) => n.as_i64().unwrap_or(0) != 0,
        _ => false,
    };
    motivos || entrada.get("state").and_then(Value::as_i64) == Some(0)
}

/// O que UM perfil diz sobre a extensão da pasta dada. `None` = não está neste perfil.
pub fn situacao_no_perfil(preferencias: &Value, pasta: &str) -> Option<Situacao> {
    let entradas = preferencias.pointer("/extensions/settings")?.as_object()?;
    let nossa = entradas
        .values()
        .find(|e| e.get("path").and_then(Value::as_str).is_some_and(|p| mesma_pasta(p, pasta)))?;
    let modo_dev = preferencias.pointer("/extensions/ui/developer_mode").and_then(Value::as_bool).unwrap_or(false);
    Some(if !modo_dev {
        Situacao::ModoDevDesligado
    } else if desativada(nossa) {
        Situacao::Desativada
    } else {
        Situacao::Carregada
    })
}

/// Pasta "User Data" do Chrome deste usuário.
pub fn dados_do_chrome() -> PathBuf {
    std::env::var_os("LOCALAPPDATA").map(PathBuf::from).unwrap_or_default().join(r"Google\Chrome\User Data")
}

/// Junta os perfis: basta UM com a extensão em ordem. `None` = o Chrome nunca foi usado aqui.
pub fn situacao(dados: &Path, pasta_extensao: &Path) -> Option<Situacao> {
    let pasta = pasta_extensao.to_string_lossy();
    let mut melhor: Option<Situacao> = None;
    let mut havia_perfil = false;
    for perfil in fs::read_dir(dados).ok()?.filter_map(|e| e.ok()) {
        let Ok(bruto) = fs::read(perfil.path().join("Secure Preferences")) else {
            continue;
        };
        havia_perfil = true;
        let Ok(preferencias) = serde_json::from_slice::<Value>(&bruto) else {
            continue; // o Chrome está regravando o arquivo: o próximo ciclo lê de novo
        };
        match situacao_no_perfil(&preferencias, &pasta) {
            Some(Situacao::Carregada) => return Some(Situacao::Carregada),
            Some(s) => melhor = Some(s),
            None => {}
        }
    }
    havia_perfil.then(|| melhor.unwrap_or(Situacao::NaoCarregada))
}

/// Há um chrome.exe rodando NESTA sessão do Windows (a deste usuário)?
pub fn chrome_aberto() -> bool {
    unsafe {
        let mut minha = 0u32;
        ProcessIdToSessionId(GetCurrentProcessId(), &mut minha);
        let foto = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
        if foto == INVALID_HANDLE_VALUE {
            return false;
        }
        let mut p: PROCESSENTRY32W = std::mem::zeroed();
        p.dwSize = std::mem::size_of::<PROCESSENTRY32W>() as u32;
        let mut achou = false;
        let mut segue = Process32FirstW(foto, &mut p);
        while segue != 0 {
            let fim = p.szExeFile.iter().position(|c| *c == 0).unwrap_or(p.szExeFile.len());
            if String::from_utf16_lossy(&p.szExeFile[..fim]).eq_ignore_ascii_case("chrome.exe") {
                let mut sessao = 0u32;
                if ProcessIdToSessionId(p.th32ProcessID, &mut sessao) != 0 && sessao == minha {
                    achou = true;
                    break;
                }
            }
            segue = Process32NextW(foto, &mut p);
        }
        CloseHandle(foto);
        achou
    }
}

#[cfg(test)]
mod testes {
    use super::*;
    use serde_json::json;

    const PASTA: &str = r"C:\SMSMais\extensao";

    fn perfil(modo_dev: bool, extra: Value) -> Value {
        let mut nossa = json!({ "location": 4, "path": "C:\\SMSMais\\extensao", "from_webstore": false });
        for (k, v) in extra.as_object().unwrap() {
            nossa[k] = v.clone();
        }
        json!({ "extensions": {
            "ui": { "developer_mode": modo_dev },
            "settings": {
                "aaaa": { "location": 1, "path": "aaaa\\1.0_0" },
                "bbbb": nossa,
            }
        }})
    }

    #[test]
    fn carregada_e_ativa() {
        assert_eq!(situacao_no_perfil(&perfil(true, json!({})), PASTA), Some(Situacao::Carregada));
        // a pasta bate mesmo com barra no fim, barras trocadas ou caixa diferente
        assert_eq!(situacao_no_perfil(&perfil(true, json!({})), "c:/smsmais/EXTENSAO/"), Some(Situacao::Carregada));
        assert_eq!(situacao_no_perfil(&perfil(true, json!({ "state": 1, "disable_reasons": [] })), PASTA), Some(Situacao::Carregada));
    }

    #[test]
    fn modo_desenvolvedor_desligado_tem_aviso_proprio() {
        assert_eq!(situacao_no_perfil(&perfil(false, json!({})), PASTA), Some(Situacao::ModoDevDesligado));
        let sem_ui = json!({ "extensions": { "settings": { "x": { "path": PASTA } } } });
        assert_eq!(situacao_no_perfil(&sem_ui, PASTA), Some(Situacao::ModoDevDesligado));
    }

    #[test]
    fn desativada_nos_formatos_novo_e_antigo() {
        assert_eq!(situacao_no_perfil(&perfil(true, json!({ "disable_reasons": [1] })), PASTA), Some(Situacao::Desativada));
        assert_eq!(situacao_no_perfil(&perfil(true, json!({ "disable_reasons": 1 })), PASTA), Some(Situacao::Desativada));
        assert_eq!(situacao_no_perfil(&perfil(true, json!({ "state": 0 })), PASTA), Some(Situacao::Desativada));
    }

    #[test]
    fn perfil_sem_a_extensao() {
        assert_eq!(situacao_no_perfil(&perfil(true, json!({})), r"C:\outra\pasta"), None);
        assert_eq!(situacao_no_perfil(&json!({}), PASTA), None);
        // pasta parecida não é a mesma pasta
        assert_eq!(situacao_no_perfil(&perfil(true, json!({})), r"C:\SMSMais\extensao-sisreg"), None);
    }

    #[test]
    fn basta_um_perfil_em_ordem() {
        let dados = std::env::temp_dir().join(format!("smsmais-atualizador-teste-chrome-{}", std::process::id()));
        let _ = fs::remove_dir_all(&dados);
        assert_eq!(situacao(&dados, Path::new(PASTA)), None, "sem Chrome neste usuário não há o que avisar");

        let gravar = |nome: &str, v: &Value| {
            fs::create_dir_all(dados.join(nome)).unwrap();
            fs::write(dados.join(nome).join("Secure Preferences"), v.to_string()).unwrap();
        };
        gravar("Default", &json!({ "extensions": { "ui": { "developer_mode": true }, "settings": {} } }));
        assert_eq!(situacao(&dados, Path::new(PASTA)), Some(Situacao::NaoCarregada));
        gravar("Profile 1", &perfil(true, json!({ "disable_reasons": [1] })));
        assert_eq!(situacao(&dados, Path::new(PASTA)), Some(Situacao::Desativada));
        gravar("Profile 2", &perfil(true, json!({})));
        assert_eq!(situacao(&dados, Path::new(PASTA)), Some(Situacao::Carregada));
        let _ = fs::remove_dir_all(&dados);
    }
}
