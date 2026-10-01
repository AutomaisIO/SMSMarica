// A extensão do Chrome: conferir a versão publicada, baixar o pacote e trocar os arquivos da pasta
// que o Chrome carrega ("Carregar sem compactação").
//
// Quem recarrega a extensão é ELA MESMA: o service worker compara a versão do manifest.json que
// está no disco com a que está rodando e chama chrome.runtime.reload(). Por isso o manifest.json
// é sempre o ÚLTIMO arquivo trocado — é ele que dá o sinal, e nessa hora o resto já está no lugar.

use std::collections::BTreeSet;
use std::fs;
use std::io::{Cursor, Read};
use std::path::{Path, PathBuf};
use std::thread::sleep;
use std::time::Duration;

use serde_json::{json, Value};

use crate::config::{gravar_estado, ler_estado, Config};
use crate::plataforma::buscar;
use crate::versao::mais_nova;

const MANIFESTO: &str = "manifest.json";
const LIMITE_MANIFESTO: usize = 1024 * 1024;
const LIMITE_ZIP: usize = 50 * 1024 * 1024;
const LIMITE_DESCOMPACTADO: u64 = 100 * 1024 * 1024;
/// Sufixo do arquivo temporário da troca (gravar ao lado e renomear por cima = troca atômica).
const SUFIXO_TROCA: &str = ".atualizando";

#[derive(Debug)]
pub struct Pacote {
    pub versao: String,
    /// Caminho relativo com "/" → conteúdo.
    pub arquivos: Vec<(String, Vec<u8>)>,
}

pub fn versao_do_manifesto(bytes: &[u8]) -> Option<String> {
    let v: Value = serde_json::from_slice(bytes).ok()?;
    v.get("version")?.as_str().map(str::trim).filter(|s| !s.is_empty()).map(String::from)
}

pub fn versao_local(pasta: &Path) -> Option<String> {
    versao_do_manifesto(&fs::read(pasta.join(MANIFESTO)).ok()?)
}

/// O que vem no repositório de distribuição mas não é da extensão (ou é nosso temporário).
fn ignorado(partes: &[String]) -> bool {
    if partes.iter().any(|p| p.starts_with('.')) {
        return true; // .git, .github, .gitignore…
    }
    let nome = partes.last().map(|n| n.to_lowercase()).unwrap_or_default();
    nome == "atualizador.json"
        || nome.ends_with(SUFIXO_TROCA)
        || [".bat", ".cmd", ".ps1", ".exe"].iter().any(|ext| nome.ends_with(ext))
}

/// Arquivos que o manifest manda o Chrome carregar. Faltando um, a extensão recarregada não sobe
/// — e sem service worker ela não se conserta sozinha. Melhor recusar o pacote.
fn referenciados(manifesto: &Value) -> Vec<String> {
    let mut r = Vec::new();
    let mut por = |v: Option<&Value>| {
        if let Some(s) = v.and_then(Value::as_str) {
            r.push(s.trim_start_matches("./").trim_start_matches('/').to_string());
        }
    };
    por(manifesto.pointer("/background/service_worker"));
    por(manifesto.pointer("/action/default_popup"));
    por(manifesto.pointer("/options_page"));
    if let Some(scripts) = manifesto.get("content_scripts").and_then(Value::as_array) {
        for cs in scripts {
            for lista in ["js", "css"] {
                for arq in cs.get(lista).and_then(Value::as_array).into_iter().flatten() {
                    por(Some(arq));
                }
            }
        }
    }
    r
}

/// Lê o .zip (o "Download ZIP" do GitHub traz tudo dentro de uma pasta `repo-branch/`; essa pasta
/// de cima é descartada), filtra o que não é da extensão e confere se o pacote se sustenta.
pub fn abrir_pacote(zip: &[u8]) -> Result<Pacote, String> {
    let mut arquivo = zip::ZipArchive::new(Cursor::new(zip)).map_err(|e| format!("pacote ilegível: {e}"))?;
    let mut entradas: Vec<(Vec<String>, Vec<u8>)> = Vec::new();
    let mut total = 0u64;

    for i in 0..arquivo.len() {
        let mut f = arquivo.by_index(i).map_err(|e| format!("pacote ilegível: {e}"))?;
        if f.is_dir() {
            continue;
        }
        // `enclosed_name` recusa caminho absoluto e "..": um pacote assim não é nosso.
        let caminho = f
            .enclosed_name()
            .ok_or_else(|| format!("pacote recusado: caminho inválido \"{}\"", f.name()))?;
        let partes: Vec<String> = caminho.iter().map(|p| p.to_string_lossy().into_owned()).collect();
        if partes.is_empty() {
            continue;
        }
        total += f.size();
        if total > LIMITE_DESCOMPACTADO {
            return Err("pacote recusado: grande demais".into());
        }
        let mut conteudo = Vec::with_capacity(f.size() as usize);
        (&mut f)
            .take(LIMITE_DESCOMPACTADO)
            .read_to_end(&mut conteudo)
            .map_err(|e| format!("pacote ilegível em \"{}\": {e}", partes.join("/")))?;
        entradas.push((partes, conteudo));
    }

    // Pasta de cima comum a tudo (extensao-sisreg-main/…) sai do caminho.
    let comum = entradas.first().map(|(p, _)| p[0].clone());
    if let Some(comum) = comum {
        if entradas.iter().all(|(p, _)| p.len() > 1 && p[0] == comum) {
            for (p, _) in &mut entradas {
                p.remove(0);
            }
        }
    }

    let arquivos: Vec<(String, Vec<u8>)> = entradas
        .into_iter()
        .filter(|(p, _)| !ignorado(p))
        .map(|(p, c)| (p.join("/"), c))
        .collect();

    let bruto = arquivos
        .iter()
        .find(|(n, _)| n == MANIFESTO)
        .map(|(_, c)| c.as_slice())
        .ok_or("pacote recusado: sem manifest.json")?;
    let manifesto: Value =
        serde_json::from_slice(bruto).map_err(|e| format!("pacote recusado: manifest.json inválido ({e})"))?;
    let versao = versao_do_manifesto(bruto).ok_or("pacote recusado: manifest.json sem versão")?;

    let nomes: BTreeSet<&str> = arquivos.iter().map(|(n, _)| n.as_str()).collect();
    for r in referenciados(&manifesto) {
        if !nomes.contains(r.as_str()) {
            return Err(format!("pacote recusado: o manifest pede \"{r}\" e o arquivo não veio"));
        }
    }
    Ok(Pacote { versao, arquivos })
}

fn destino(pasta: &Path, relativo: &str) -> Option<PathBuf> {
    let mut d = pasta.to_path_buf();
    for parte in relativo.split('/') {
        if parte.is_empty() || parte == "." || parte == ".." || parte.contains(['\\', ':']) {
            return None;
        }
        d.push(parte);
    }
    Some(d)
}

/// Grava ao lado e renomeia por cima: o Chrome nunca lê um arquivo pela metade. Arquivo igual ao
/// que já está lá não é tocado.
fn trocar(destino: &Path, conteudo: &[u8]) -> Result<(), String> {
    if fs::read(destino).map(|atual| atual == conteudo).unwrap_or(false) {
        return Ok(());
    }
    if let Some(pai) = destino.parent() {
        fs::create_dir_all(pai).map_err(|e| format!("não consegui criar {}: {e}", pai.display()))?;
    }
    let mut temporario = destino.as_os_str().to_owned();
    temporario.push(SUFIXO_TROCA);
    let temporario = PathBuf::from(temporario);
    fs::write(&temporario, conteudo).map_err(|e| format!("não consegui gravar {}: {e}", temporario.display()))?;

    // O arquivo pode estar aberto pelo Chrome ou pelo antivírus por um instante.
    let mut ultimo = String::new();
    for _ in 0..10 {
        match fs::rename(&temporario, destino) {
            Ok(()) => return Ok(()),
            Err(e) => ultimo = e.to_string(),
        }
        sleep(Duration::from_millis(300));
    }
    let _ = fs::remove_file(&temporario);
    Err(format!("não consegui trocar {}: {ultimo}", destino.display()))
}

/// Troca os arquivos da pasta pelos do pacote (manifest.json por último) e apaga o que o
/// atualizador tinha posto antes e não existe mais. Falhou no meio: o manifest antigo continua
/// lá, a extensão não recarrega e o próximo ciclo tenta de novo.
pub fn aplicar(pasta: &Path, pacote: &Pacote, anteriores: &[String]) -> Result<(), String> {
    fs::create_dir_all(pasta).map_err(|e| format!("não consegui criar {}: {e}", pasta.display()))?;

    let mut manifesto = None;
    for (nome, conteudo) in &pacote.arquivos {
        let d = destino(pasta, nome).ok_or_else(|| format!("caminho recusado no pacote: \"{nome}\""))?;
        if nome == MANIFESTO {
            manifesto = Some((d, conteudo));
        } else {
            trocar(&d, conteudo)?;
        }
    }
    let (d, conteudo) = manifesto.ok_or("pacote sem manifest.json")?;
    trocar(&d, conteudo)?;

    let novos: BTreeSet<&str> = pacote.arquivos.iter().map(|(n, _)| n.as_str()).collect();
    for antigo in anteriores.iter().filter(|a| !novos.contains(a.as_str())) {
        if let Some(d) = destino(pasta, antigo) {
            let _ = fs::remove_file(d);
        }
    }
    Ok(())
}

/// Arquivo aberto com exclusividade enquanto um residente troca os arquivos da extensão. `None`
/// = outro residente (outra sessão do Windows) está trocando agora.
fn trava() -> Option<fs::File> {
    use std::os::windows::fs::OpenOptionsExt;
    let caminho = crate::sistema::locais().base.join("aplicando.trava");
    fs::OpenOptions::new().create(true).write(true).truncate(false).share_mode(0).open(caminho).ok()
}

/// Só o que cabe sem escape num parâmetro de consulta (versões e rótulos fixos).
fn para_consulta(s: &str) -> String {
    s.chars().filter(|c| c.is_ascii_alphanumeric() || matches!(c, '.' | '-')).collect()
}

/// Um ciclo: `Ok(Some(versão))` quando atualizou, `Ok(None)` quando já estava em dia (ou quando
/// este computador ainda não foi configurado e autorizado na plataforma).
///
/// A consulta leva o estado deste computador (versão instalada, versão do atualizador e a
/// situação da extensão no Chrome): é o inventário que a plataforma mostra no painel. Quem decide
/// a versão que este computador enxerga é a plataforma, pelo canal em que ela o pôs.
pub fn conferir(cfg: &Config, chrome: &str) -> Result<Option<String>, String> {
    let Some(origem) = cfg.origem() else {
        return Ok(None);
    };
    let local = versao_local(&cfg.pasta_extensao);
    let consulta = format!(
        "extensao/versao?instalada={}&atualizador={}&chrome={}",
        para_consulta(local.as_deref().unwrap_or("")),
        env!("CARGO_PKG_VERSION"),
        para_consulta(chrome)
    );
    let resposta = buscar(&origem, &consulta, LIMITE_MANIFESTO)?;
    let publicada = versao_do_manifesto(&resposta).ok_or("a versão publicada da extensão veio ilegível")?;
    let canal = serde_json::from_slice::<Value>(&resposta).ok().and_then(|v| v.get("canal").cloned());
    if let Some(canal) = canal.filter(Value::is_string) {
        gravar_estado(&[("canal", canal)]);
    }
    if let Some(l) = &local {
        if !mais_nova(&publicada, l) {
            return Ok(None);
        }
    }

    let pacote = abrir_pacote(&buscar(&origem, "extensao/pacote", LIMITE_ZIP)?)?;
    if let Some(l) = &local {
        if !mais_nova(&pacote.versao, l) {
            return Err(format!(
                "a versão publicada é {publicada}, mas o pacote veio na {} (instalada: {l})",
                pacote.versao
            ));
        }
    }
    // A pasta é comum a todos os usuários do PC: com dois logados ao mesmo tempo há dois
    // residentes, e só um troca os arquivos por vez. O outro vê a versão nova no próximo ciclo.
    let Some(_trava) = trava() else {
        return Ok(None);
    };
    if versao_local(&cfg.pasta_extensao).is_some_and(|l| !mais_nova(&pacote.versao, &l)) {
        return Ok(None); // o outro residente acabou de aplicar esta versão
    }
    aplicar(&cfg.pasta_extensao, &pacote, &ler_estado().arquivos)?;
    let nomes: Vec<String> = pacote.arquivos.iter().map(|(n, _)| n.clone()).collect();
    gravar_estado(&[("versao_extensao", json!(pacote.versao)), ("arquivos", json!(nomes))]);
    Ok(Some(pacote.versao))
}

#[cfg(test)]
mod testes {
    use super::*;
    use std::io::Write;
    use zip::write::SimpleFileOptions;

    fn zipar(arquivos: &[(&str, &str)]) -> Vec<u8> {
        let mut z = zip::ZipWriter::new(Cursor::new(Vec::new()));
        let op = SimpleFileOptions::default().compression_method(zip::CompressionMethod::Deflated);
        for (nome, conteudo) in arquivos {
            z.start_file(*nome, op).unwrap();
            z.write_all(conteudo.as_bytes()).unwrap();
        }
        z.finish().unwrap().into_inner()
    }

    const MANIFESTO_OK: &str = r#"{
        "manifest_version": 3, "version": "0.5.27",
        "background": { "service_worker": "background.js", "type": "module" },
        "action": { "default_popup": "popup.html" },
        "content_scripts": [ { "matches": ["https://exemplo/*"], "js": ["content.js"] } ]
    }"#;

    fn pasta_temporaria(nome: &str) -> PathBuf {
        let p = std::env::temp_dir().join(format!("smsmais-atualizador-teste-{nome}-{}", std::process::id()));
        let _ = fs::remove_dir_all(&p);
        fs::create_dir_all(&p).unwrap();
        p
    }

    #[test]
    fn pacote_do_github_perde_a_pasta_de_cima_e_o_que_nao_e_da_extensao() {
        let zip = zipar(&[
            ("extensao-sisreg-main/manifest.json", MANIFESTO_OK),
            ("extensao-sisreg-main/background.js", "// sw"),
            ("extensao-sisreg-main/content.js", "// cs"),
            ("extensao-sisreg-main/popup.html", "<html>"),
            ("extensao-sisreg-main/icones/16.png", "png"),
            ("extensao-sisreg-main/README.md", "# leia"),
            ("extensao-sisreg-main/atualizar-extensao.bat", "@echo off"),
            ("extensao-sisreg-main/atualizador.json", "{}"),
            ("extensao-sisreg-main/.gitignore", "x"),
            ("extensao-sisreg-main/.github/workflows/ci.yml", "x"),
        ]);
        let p = abrir_pacote(&zip).unwrap();
        assert_eq!(p.versao, "0.5.27");
        let nomes: Vec<&str> = p.arquivos.iter().map(|(n, _)| n.as_str()).collect();
        assert_eq!(nomes, ["manifest.json", "background.js", "content.js", "popup.html", "icones/16.png", "README.md"]);
    }

    #[test]
    fn pacote_sem_pasta_de_cima_tambem_vale() {
        let zip = zipar(&[
            ("manifest.json", MANIFESTO_OK),
            ("background.js", ""),
            ("content.js", ""),
            ("popup.html", ""),
        ]);
        assert_eq!(abrir_pacote(&zip).unwrap().arquivos.len(), 4);
    }

    #[test]
    fn recusa_pacote_que_nao_se_sustenta() {
        // falta um arquivo que o manifest manda carregar
        let falta = zipar(&[("r/manifest.json", MANIFESTO_OK), ("r/background.js", ""), ("r/popup.html", "")]);
        assert!(abrir_pacote(&falta).unwrap_err().contains("content.js"));
        // manifest quebrado
        let quebrado = zipar(&[("r/manifest.json", "{ \"version\": "), ("r/background.js", "")]);
        assert!(abrir_pacote(&quebrado).unwrap_err().contains("manifest.json inválido"));
        // sem manifest
        assert!(abrir_pacote(&zipar(&[("r/background.js", "")])).unwrap_err().contains("sem manifest.json"));
        // não é zip (página de erro, por exemplo)
        assert!(abrir_pacote(b"<html>429 Too Many Requests</html>").is_err());
    }

    #[test]
    fn recusa_caminho_que_sai_da_pasta() {
        let zip = zipar(&[
            ("r/manifest.json", MANIFESTO_OK),
            ("r/background.js", ""),
            ("r/content.js", ""),
            ("r/popup.html", ""),
            ("r/../../fora.js", "x"),
        ]);
        assert!(abrir_pacote(&zip).unwrap_err().contains("caminho inválido"));
        assert!(destino(Path::new(r"C:\x"), "../fora.js").is_none());
        assert!(destino(Path::new(r"C:\x"), "a/../../fora.js").is_none());
        assert!(destino(Path::new(r"C:\x"), r"a\..\fora.js").is_none());
        assert!(destino(Path::new(r"C:\x"), "C:/fora.js").is_none());
        assert_eq!(destino(Path::new(r"C:\x"), "icones/16.png").unwrap(), Path::new(r"C:\x\icones\16.png"));
    }

    #[test]
    fn aplicar_troca_cria_e_apaga_so_o_que_era_dele() {
        let pasta = pasta_temporaria("aplicar");
        fs::write(pasta.join("background.js"), "antigo").unwrap();
        fs::write(pasta.join("prime.js"), "saiu nesta versão").unwrap();
        fs::write(pasta.join("anotacao-do-usuario.txt"), "não é do atualizador").unwrap();

        let pacote = Pacote {
            versao: "0.5.27".into(),
            arquivos: vec![
                ("manifest.json".into(), MANIFESTO_OK.into()),
                ("background.js".into(), "novo".into()),
                ("icones/16.png".into(), "png".into()),
            ],
        };
        aplicar(&pasta, &pacote, &["background.js".into(), "prime.js".into(), "../fora.txt".into()]).unwrap();

        assert_eq!(fs::read_to_string(pasta.join("background.js")).unwrap(), "novo");
        assert_eq!(fs::read_to_string(pasta.join("icones").join("16.png")).unwrap(), "png");
        assert_eq!(versao_local(&pasta).as_deref(), Some("0.5.27"));
        assert!(!pasta.join("prime.js").exists(), "arquivo que saiu da versão deve ser apagado");
        assert!(pasta.join("anotacao-do-usuario.txt").exists(), "arquivo alheio não se toca");
        let sobras: Vec<_> = fs::read_dir(&pasta)
            .unwrap()
            .filter_map(|e| e.ok())
            .filter(|e| e.file_name().to_string_lossy().ends_with(SUFIXO_TROCA))
            .collect();
        assert!(sobras.is_empty(), "não pode sobrar temporário da troca");
        let _ = fs::remove_dir_all(&pasta);
    }

    #[test]
    fn consulta_so_leva_o_que_nao_precisa_de_escape() {
        assert_eq!(para_consulta("0.5.28"), "0.5.28");
        assert_eq!(para_consulta("modo-dev-desligado"), "modo-dev-desligado");
        assert_eq!(para_consulta("1.0&x=1 #?/"), "1.0x1");
    }

    #[test]
    fn versao_do_manifesto_tolera_lixo() {
        assert_eq!(versao_do_manifesto(MANIFESTO_OK.as_bytes()).as_deref(), Some("0.5.27"));
        assert_eq!(versao_do_manifesto(b"{}"), None);
        assert_eq!(versao_do_manifesto(b"nao e json"), None);
        assert_eq!(versao_do_manifesto(br#"{"version": 5}"#), None);
    }
}
