// Configuração (config.json na pasta do atualizador) e estado (estado.json).
//
// O executável é igual para todo município; o que é de cada instalação fica aqui: o endereço da
// API da plataforma, o token deste computador (cifrado pelo Windows) e a pasta da extensão.
// TUDO vem da plataforma — o conteúdo da extensão e as versões novas do próprio atualizador —,
// e só para computador autorizado. Sem isso, o atualizador não tem o que buscar.

use std::fs;
use std::path::{Path, PathBuf};

use serde_json::{json, Value};

use crate::sistema::{locais, revelar};

const INTERVALO_PADRAO_MIN: u64 = 10;

pub struct Config {
    pub pasta_extensao: PathBuf,
    pub intervalo_min: u64,
    /// Endereço da API da plataforma do município (sem barra no fim).
    pub plataforma: Option<String>,
    /// Token deste computador, emitido pela plataforma quando alguém logado o autorizou.
    pub token: Option<String>,
    /// Pasta "User Data" do navegador, onde o atualizador lê se a extensão está carregada.
    pub dados_do_chrome: PathBuf,
}

/// A plataforma, vista por um computador autorizado.
pub struct Origem {
    pub api: String,
    pub token: String,
}

impl Origem {
    pub fn url(&self, caminho: &str) -> String {
        format!("{}/{caminho}", self.api)
    }
}

fn ler_json(caminho: &Path) -> Value {
    fs::read(caminho)
        .ok()
        .and_then(|b| serde_json::from_slice(&b).ok())
        .unwrap_or(Value::Null)
}

fn texto(v: &Value, chave: &str) -> Option<String> {
    v.get(chave).and_then(Value::as_str).map(str::trim).filter(|s| !s.is_empty()).map(String::from)
}

impl Config {
    /// Relida a cada ciclo: mudar o config.json vale sem reiniciar o residente.
    pub fn ler() -> Config {
        Config::de(&ler_json(&locais().config))
    }

    fn de(v: &Value) -> Config {
        Config {
            pasta_extensao: texto(v, "pasta_extensao")
                .map(PathBuf::from)
                .unwrap_or_else(|| locais().extensao_padrao.clone()),
            intervalo_min: v
                .get("intervalo_minutos")
                .and_then(Value::as_u64)
                .filter(|m| *m >= 1)
                .unwrap_or(INTERVALO_PADRAO_MIN),
            plataforma: texto(v, "plataforma_api").map(|p| p.trim_end_matches('/').to_string()),
            token: texto(v, "token_protegido").and_then(|t| revelar(&t)),
            dados_do_chrome: texto(v, "chrome_user_data")
                .map(PathBuf::from)
                .unwrap_or_else(crate::navegador::dados_do_chrome),
        }
    }

    pub fn origem(&self) -> Option<Origem> {
        Some(Origem { api: self.plataforma.clone()?, token: self.token.clone()? })
    }

    /// Grava os campos dados, preservando o que mais houver no arquivo. `Value::Null` apaga.
    pub fn gravar(campos: &[(&str, Value)]) -> Result<(), String> {
        let l = locais();
        let mut v = ler_json(&l.config);
        if !v.is_object() {
            v = json!({});
        }
        for (chave, valor) in campos {
            if valor.is_null() {
                v.as_object_mut().map(|o| o.remove(*chave));
            } else {
                v[*chave] = valor.clone();
            }
        }
        let corpo = serde_json::to_vec_pretty(&v).map_err(|e| e.to_string())?;
        fs::create_dir_all(&l.base).map_err(|e| format!("não consegui criar {}: {e}", l.base.display()))?;
        // Grava ao lado e renomeia: o residente relê este arquivo a cada ciclo.
        let temporario = l.config.with_extension("json.novo");
        fs::write(&temporario, corpo).map_err(|e| format!("não consegui gravar {}: {e}", temporario.display()))?;
        fs::rename(&temporario, &l.config).map_err(|e| format!("não consegui gravar {}: {e}", l.config.display()))
    }
}

/// O que o atualizador lembra entre uma execução e outra.
pub struct Estado {
    /// Arquivos que ele pôs na pasta da extensão na última vez — é o que permite apagar o que saiu
    /// de uma versão para a outra sem encostar em arquivo que não é dele.
    pub arquivos: Vec<String>,
    /// Canal em que a plataforma pôs este computador (só para mostrar; quem decide é ela).
    pub canal: Option<String>,
}

pub fn ler_estado() -> Estado {
    let v = ler_json(&locais().estado);
    Estado {
        arquivos: v
            .get("arquivos")
            .and_then(Value::as_array)
            .map(|a| a.iter().filter_map(Value::as_str).map(String::from).collect())
            .unwrap_or_default(),
        canal: texto(&v, "canal"),
    }
}

/// Grava os campos dados no estado, preservando os demais.
pub fn gravar_estado(campos: &[(&str, Value)]) {
    let caminho = &locais().estado;
    let mut v = ler_json(caminho);
    if !v.is_object() {
        v = json!({});
    }
    for (chave, valor) in campos {
        v[*chave] = valor.clone();
    }
    if let Ok(corpo) = serde_json::to_vec_pretty(&v) {
        let _ = fs::write(caminho, corpo);
    }
}

#[cfg(test)]
mod testes {
    use super::*;
    use crate::sistema::proteger;

    #[test]
    fn sem_plataforma_nao_ha_o_que_buscar() {
        let c = Config::de(&json!({}));
        assert!(c.origem().is_none());
        assert_eq!(c.intervalo_min, INTERVALO_PADRAO_MIN);
    }

    #[test]
    fn plataforma_sem_autorizacao_tambem_nao_busca() {
        let c = Config::de(&json!({ "plataforma_api": "https://api.exemplo.gov.br/" }));
        assert_eq!(c.plataforma.as_deref(), Some("https://api.exemplo.gov.br"));
        assert!(c.origem().is_none(), "sem token, a plataforma recusaria");
        // token que não foi cifrado pelo Windows deste computador não vale
        let c = Config::de(&json!({ "plataforma_api": "https://api.exemplo.gov.br", "token_protegido": "00ff" }));
        assert!(c.token.is_none() && c.origem().is_none());
    }

    #[test]
    fn origem_de_computador_autorizado() {
        let c = Config::de(&json!({
            "pasta_extensao": r"D:\ext",
            "intervalo_minutos": 3,
            "plataforma_api": "https://api.exemplo.gov.br",
            "token_protegido": proteger("abc").unwrap(),
        }));
        let o = c.origem().unwrap();
        assert_eq!(o.url("extensao/versao"), "https://api.exemplo.gov.br/extensao/versao");
        assert_eq!(o.token, "abc");
        assert_eq!(c.pasta_extensao, PathBuf::from(r"D:\ext"));
        assert_eq!(c.intervalo_min, 3);
        // intervalo zero cairia num laço apertado: volta ao padrão
        assert_eq!(Config::de(&json!({ "intervalo_minutos": 0 })).intervalo_min, INTERVALO_PADRAO_MIN);
    }
}
