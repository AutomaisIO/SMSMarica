// O atualizador atualiza a si mesmo. A plataforma diz qual é a versão publicada para este
// computador (`extensao/atualizador/versao` → versão + SHA-256) e entrega o executável
// (`extensao/atualizador/pacote`), sempre com o token do computador.
//
// O Windows não deixa gravar por cima de um executável em uso, mas deixa RENOMEAR. Então: baixa o
// novo ao lado, confere o SHA-256, prova que ele roda nesta máquina, renomeia o atual para
// ".antigo.exe", põe o novo no lugar e o residente renasce do executável novo.

use std::fs;
use std::io::Read;
use std::os::windows::process::CommandExt;
use std::path::{Path, PathBuf};
use std::process::{Command, Stdio};
use std::thread::sleep;
use std::time::{Duration, Instant};

use serde_json::Value;
use sha2::{Digest, Sha256};

use crate::config::Config;
use crate::plataforma::buscar;
use crate::sistema::{self, locais, mesmo_arquivo};
use crate::versao::mais_nova;

pub const VERSAO: &str = env!("CARGO_PKG_VERSION");
const LIMITE_DESCRICAO: usize = 64 * 1024;
const LIMITE_EXE: usize = 30 * 1024 * 1024;
const CREATE_NO_WINDOW: u32 = 0x0800_0000;
const DETACHED_PROCESS: u32 = 0x0000_0008;
const CREATE_NEW_PROCESS_GROUP: u32 = 0x0000_0200;

fn ao_lado(marca: &str) -> PathBuf {
    locais().base.join(format!("smsmais-atualizador.{marca}.exe"))
}

/// Restos de uma troca anterior, na partida do residente.
pub fn limpar_restos() {
    limpar_antigo();
    let _ = fs::remove_file(ao_lado("novo"));
}

/// O ".antigo" só pode ser apagado depois que o processo velho sai — e o residente novo nasce com
/// ele ainda saindo. Por isso a tentativa se repete a cada ciclo (medido: na partida falha).
pub fn limpar_antigo() {
    let _ = fs::remove_file(ao_lado("antigo"));
}

// ------------------------------------------------------------ o que vem no instalador
// O instalador baixado pelo painel é este mesmo executável com um rabicho no fim do arquivo: o
// endereço da API e um código de ativação de uso único, emitido para quem estava logado e clicou
// em baixar. Assim a pessoa baixa, executa e pronto — sem digitar endereço nem senha.
//
//   <bytes do executável>\n#SMSMAIS-INSTALADOR:{"api":"https://api...","codigo":"..."}

/// A marca é montada em tempo de execução para a sequência inteira não existir dentro do próprio
/// executável (senão ele se acharia "com rabicho" sem ter).
fn marca() -> Vec<u8> {
    let mut m = b"\n#SMSMAIS-".to_vec();
    m.extend_from_slice(b"INSTALADOR:");
    m
}

/// Onde o rabicho começa (procurado só no fim do arquivo).
fn inicio_do_rabicho(arquivo: &[u8]) -> Option<usize> {
    let marca = marca();
    let janela = arquivo.len().saturating_sub(4096);
    arquivo[janela..].windows(marca.len()).rposition(|w| w == marca.as_slice()).map(|i| janela + i)
}

pub struct Embutido {
    pub api: String,
    pub codigo: String,
}

pub fn ler_embutido(arquivo: &[u8]) -> Option<Embutido> {
    let i = inicio_do_rabicho(arquivo)?;
    let v: Value = serde_json::from_slice(&arquivo[i + marca().len()..]).ok()?;
    let campo = |n: &str| v.get(n).and_then(Value::as_str).map(str::trim).filter(|s| !s.is_empty()).map(String::from);
    Some(Embutido { api: campo("api")?.trim_end_matches('/').to_string(), codigo: campo("codigo")? })
}

/// O executável sem o rabicho — é assim que ele é instalado (o código de ativação é de uso
/// único e não tem por que ficar no disco).
pub fn sem_rabicho(arquivo: &[u8]) -> &[u8] {
    match inicio_do_rabicho(arquivo) {
        Some(i) => &arquivo[..i],
        None => arquivo,
    }
}

// ------------------------------------------------------------------ auto-atualização
#[derive(Debug)]
pub struct Publicado {
    pub versao: String,
    pub sha256: String,
}

pub fn ler_descricao(bruto: &[u8]) -> Result<Publicado, String> {
    let v: Value = serde_json::from_slice(bruto).map_err(|e| format!("descrição do atualizador ilegível ({e})"))?;
    let campo = |nome: &str| {
        v.get(nome)
            .and_then(Value::as_str)
            .map(str::trim)
            .filter(|s| !s.is_empty())
            .map(String::from)
            .ok_or_else(|| format!("descrição do atualizador sem \"{nome}\""))
    };
    let p = Publicado { versao: campo("versao")?, sha256: campo("sha256")?.to_lowercase() };
    if p.sha256.len() != 64 || !p.sha256.bytes().all(|b| b.is_ascii_hexdigit()) {
        return Err("descrição do atualizador com \"sha256\" malformado".into());
    }
    Ok(p)
}

pub fn sha256_hex(dados: &[u8]) -> String {
    Sha256::digest(dados).iter().map(|b| format!("{b:02x}")).collect()
}

/// Roda `<exe> --versao` e confere a resposta: prova que o executável baixado abre NESTA máquina
/// (antivírus, arquitetura, arquivo truncado) antes de ele virar o executável oficial.
fn ensaiar(exe: &Path, esperada: &str) -> Result<(), String> {
    let respondeu = versao_de(exe)?;
    if respondeu == esperada {
        Ok(())
    } else {
        Err(format!("o executável novo diz ser \"{respondeu}\", esperado \"{esperada}\""))
    }
}

/// Versão do executável que está no lugar de instalação (`None` = não há, ou não respondeu).
pub fn versao_instalada() -> Option<String> {
    let instalado = &locais().exe;
    if !instalado.is_file() {
        return None;
    }
    versao_de(instalado).ok().filter(|v| !v.is_empty())
}

/// O que `<exe> --versao` responde.
fn versao_de(exe: &Path) -> Result<String, String> {
    let mut filho = None;
    let mut ultimo = String::new();
    for _ in 0..5 {
        // Executável recém-gravado costuma estar preso pelo antivírus por um instante.
        match Command::new(exe)
            .arg("--versao")
            .stdin(Stdio::null())
            .stdout(Stdio::piped())
            .stderr(Stdio::null())
            .creation_flags(CREATE_NO_WINDOW)
            .spawn()
        {
            Ok(f) => {
                filho = Some(f);
                break;
            }
            Err(e) => ultimo = e.to_string(),
        }
        sleep(Duration::from_millis(800));
    }
    let mut filho = filho.ok_or_else(|| format!("o executável novo não abriu ({ultimo})"))?;

    let inicio = Instant::now();
    loop {
        match filho.try_wait() {
            Ok(Some(_)) => break,
            Ok(None) if inicio.elapsed() > Duration::from_secs(20) => {
                let _ = filho.kill();
                return Err("o executável novo não respondeu".into());
            }
            Ok(None) => sleep(Duration::from_millis(100)),
            Err(e) => return Err(format!("o executável novo não respondeu ({e})")),
        }
    }
    let mut saida = String::new();
    if let Some(mut s) = filho.stdout.take() {
        let _ = s.read_to_string(&mut saida);
    }
    Ok(saida.trim().to_string())
}

/// `Ok(Some(versão))` = o executável instalado foi trocado; quem chamou deve renascer dele.
pub fn conferir(cfg: &Config) -> Result<Option<String>, String> {
    // Só o executável instalado se atualiza — uma cópia rodada de outra pasta não mexe em nada.
    let instalado = &locais().exe;
    let este = std::env::current_exe().map_err(|e| e.to_string())?;
    if !mesmo_arquivo(&este, instalado) {
        return Ok(None);
    }

    // A pasta é comum: o residente de OUTRO usuário (ou uma reinstalação) pode já ter trocado o
    // executável do disco enquanto este processo seguia rodando a imagem antiga. Nesse caso não
    // há o que baixar — é só renascer do que está lá.
    if let Ok(no_disco) = versao_de(instalado) {
        if mais_nova(&no_disco, VERSAO) {
            return Ok(Some(no_disco));
        }
    }

    let Some(origem) = cfg.origem() else {
        return Ok(None);
    };
    let publicado = ler_descricao(&buscar(&origem, "extensao/atualizador/versao", LIMITE_DESCRICAO)?)?;
    if !mais_nova(&publicado.versao, VERSAO) {
        return Ok(None);
    }

    let exe = buscar(&origem, "extensao/atualizador/pacote", LIMITE_EXE)?;
    let sha = sha256_hex(&exe);
    if sha != publicado.sha256 {
        return Err(format!(
            "atualizador v{} recusado: SHA-256 {sha} não confere com o publicado {}",
            publicado.versao, publicado.sha256
        ));
    }
    if !exe.starts_with(b"MZ") {
        return Err(format!("atualizador v{} recusado: não é um executável", publicado.versao));
    }

    let novo = ao_lado("novo");
    let antigo = ao_lado("antigo");
    fs::write(&novo, &exe).map_err(|e| format!("não consegui gravar {}: {e}", novo.display()))?;
    if let Err(e) = ensaiar(&novo, &publicado.versao) {
        let _ = fs::remove_file(&novo);
        return Err(format!("atualizador v{} recusado: {e}", publicado.versao));
    }

    let _ = fs::remove_file(&antigo);
    if let Err(e) = fs::rename(instalado, &antigo) {
        let _ = fs::remove_file(&novo);
        return Err(format!("não consegui tirar o executável atual do lugar: {e}"));
    }
    if let Err(e) = fs::rename(&novo, instalado) {
        let _ = fs::rename(&antigo, instalado); // desfaz: volta o que estava
        let _ = fs::remove_file(&novo);
        return Err(format!("não consegui pôr o executável novo no lugar: {e}"));
    }
    Ok(Some(publicado.versao))
}

/// Sobe o residente a partir do executável instalado, solto deste processo.
pub fn iniciar_residente(extras: &[&str]) -> Result<(), String> {
    let l = locais();
    // O residente não pode herdar a saída de quem o criou: se o instalador foi chamado por um
    // script que lê a saída dele (um pipe), o script ficaria esperando o residente — para sempre.
    sistema::nao_herdar_saida();
    Command::new(&l.exe)
        .arg("--residente")
        .args(extras)
        .current_dir(&l.base)
        .stdin(Stdio::null())
        .stdout(Stdio::null())
        .stderr(Stdio::null())
        .creation_flags(DETACHED_PROCESS | CREATE_NEW_PROCESS_GROUP)
        .spawn()
        .map(|_| ())
        .map_err(|e| format!("não consegui iniciar o atualizador: {e}"))
}

#[cfg(test)]
mod testes {
    use super::*;

    #[test]
    fn sha256_conhecido() {
        assert_eq!(sha256_hex(b"abc"), "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad");
    }

    #[test]
    fn descricao_valida_e_invalidas() {
        let sha = "BA7816BF8F01CFEA414140DE5DAE2223B00361A396177A9CB410FF61F20015AD";
        let ok = format!(r#"{{ "versao": "0.2.0", "sha256": "{sha}" }}"#);
        let p = ler_descricao(ok.as_bytes()).unwrap();
        assert_eq!(p.versao, "0.2.0");
        assert_eq!(p.sha256, sha.to_lowercase());

        assert!(ler_descricao(b"<html>404</html>").unwrap_err().contains("ilegível"));
        assert!(ler_descricao(br#"{ "versao": "0.2.0" }"#).unwrap_err().contains("sha256"));
        assert!(ler_descricao(br#"{ "versao": "0.2.0", "sha256": "abc" }"#).unwrap_err().contains("malformado"));
    }

    #[test]
    fn instalador_do_painel_traz_api_e_codigo_no_rabicho() {
        let exe = b"MZ...bytes do executavel...".to_vec();
        let mut instalador = exe.clone();
        instalador.extend_from_slice(&marca());
        instalador.extend_from_slice(br#"{"api":"https://api.exemplo.gov.br/","codigo":"abc123"}"#);

        let e = ler_embutido(&instalador).unwrap();
        assert_eq!((e.api.as_str(), e.codigo.as_str()), ("https://api.exemplo.gov.br", "abc123"));
        assert_eq!(sem_rabicho(&instalador), exe.as_slice(), "o que se instala é o executável limpo");
    }

    #[test]
    fn executavel_sem_rabicho_fica_como_esta() {
        let exe = b"MZ...bytes do executavel...".to_vec();
        assert!(ler_embutido(&exe).is_none());
        assert_eq!(sem_rabicho(&exe), exe.as_slice());
        // rabicho com JSON quebrado ou incompleto não vale como configuração
        let mut ruim = exe.clone();
        ruim.extend_from_slice(&marca());
        ruim.extend_from_slice(br#"{"api":"https://x"}"#);
        assert!(ler_embutido(&ruim).is_none());
    }

    #[test]
    fn este_executavel_nao_carrega_a_marca_inteira() {
        // Se a marca aparecesse inteira dentro do binário, ele se acharia "com rabicho".
        let este = fs::read(std::env::current_exe().unwrap()).unwrap();
        assert!(inicio_do_rabicho(&este).is_none());
    }
}
