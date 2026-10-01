// A conversa com a plataforma do município. TUDO fica hospedado nela: o instalador (baixado pelo
// painel, por quem está logado), o conteúdo da extensão e as versões novas do atualizador.
//
// O atualizador não pede senha. Quem autoriza o computador é uma pessoa logada no painel:
//   - pelo instalador: o painel entrega o executável já com o endereço da API e um código de
//     ativação de uso único (emitido para quem clicou em baixar);
//   - pelo "Configurar": o atualizador pede um código à API e abre o painel no navegador; quem
//     está logado clica em "autorizar este computador".
// Nos dois casos o código é trocado pelo token DESTE computador (revogável no painel).
//
// Contrato com a API (o lado do servidor é da plataforma):
//   GET  {api}/publico/instituicao                      → { "nomeCurto": "...", ... }       (já existe)
//   POST {api}/extensao/dispositivos/ativacoes          { "computador", "versaoAtualizador" }
//        → 200 { "codigo", "urlAutorizar", "expiraEmSegundos", "intervaloSegundos" }
//   POST {api}/extensao/dispositivos/ativacoes/token    { "codigo", "computador", "versaoAtualizador" }
//        → 200 { "token" } autorizado · 202 ainda aguardando · 410 recusada, usada ou vencida
//   Com `Authorization: Bearer <token do computador>` (401/403 = computador revogado):
//   GET  {api}/extensao/versao?instalada=&atualizador=&chrome=   → { "version", "canal" }
//   GET  {api}/extensao/pacote                                   → .zip da extensão
//   GET  {api}/extensao/atualizador/versao                       → { "versao", "sha256" }
//   GET  {api}/extensao/atualizador/pacote                       → o executável
// O CANAL (teste/prod) é atributo do computador NA PLATAFORMA: é ela que decide qual versão cada
// computador enxerga. O que está em desenvolvimento não chega a PC nenhum — só o que foi publicado.

use serde_json::{json, Value};

use crate::config::Origem;
use crate::rede::{baixar, pedir, url_permitida};
use crate::sistema::nome_do_computador;

const LIMITE: usize = 256 * 1024;

/// Começo da mensagem quando a plataforma recusa o token deste computador (revogado, vencido).
/// Quem mostra o aviso ao usuário reconhece por aqui.
pub const RECUSADO: &str = "A plataforma não reconhece mais este computador";

/// GET autenticado com o token do computador.
pub fn buscar(origem: &Origem, caminho: &str, limite: usize) -> Result<Vec<u8>, String> {
    baixar(&origem.url(caminho), limite, Some(&origem.token)).map_err(|f| match f.status {
        Some(s @ (401 | 403)) => format!("{RECUSADO} (HTTP {s}). Use Configurar para autorizar de novo."),
        _ => f.mensagem,
    })
}

fn texto(v: &Value, chave: &str) -> Option<String> {
    v.get(chave).and_then(Value::as_str).map(str::trim).filter(|s| !s.is_empty()).map(String::from)
}

/// O que a pessoa digita vira candidatos a endereço da API: aceita com ou sem "https://", e
/// aceita o endereço do PAINEL (tenta também o "api." na frente).
pub fn candidatos(digitado: &str) -> Vec<String> {
    let limpo = digitado.trim().trim_end_matches('/');
    if limpo.is_empty() {
        return Vec::new();
    }
    let com_esquema = if limpo.contains("://") { limpo.to_string() } else { format!("https://{limpo}") };
    let mut lista = vec![com_esquema.clone()];
    if let Some((esquema, resto)) = com_esquema.split_once("://") {
        let host = resto.split('/').next().unwrap_or_default();
        let ip_ou_local = host.starts_with("localhost") || host.split(':').next().unwrap_or("").parse::<std::net::Ipv4Addr>().is_ok();
        if !host.starts_with("api.") && !ip_ou_local && !resto.contains('/') {
            lista.push(format!("{esquema}://api.{resto}"));
        }
    }
    lista.retain(|u| url_permitida(u));
    lista
}

/// Acha a API a partir do que foi digitado: é o primeiro candidato que responde como plataforma.
/// Devolve (endereço da API, nome da instituição) — o nome confirma ao usuário onde ele entrou.
pub fn localizar(digitado: &str) -> Result<(String, String), String> {
    let lista = candidatos(digitado);
    if lista.is_empty() {
        return Err("Endereço inválido. Informe o endereço da plataforma (só https).".into());
    }
    let mut ultimo = String::new();
    for api in lista {
        match pedir(&format!("{api}/publico/instituicao"), None, None, LIMITE) {
            Ok(r) if r.status == 200 => {
                if let Ok(v) = serde_json::from_slice::<Value>(&r.corpo) {
                    if let Some(nome) = texto(&v, "nomeCurto").or_else(|| texto(&v, "nome")) {
                        return Ok((api, nome));
                    }
                }
                ultimo = format!("{api} respondeu, mas não é a API da plataforma");
            }
            Ok(r) => ultimo = format!("{api} respondeu HTTP {}", r.status),
            Err(e) => ultimo = e,
        }
    }
    Err(format!("Não encontrei a plataforma nesse endereço ({ultimo})."))
}

fn quem_sou() -> Value {
    json!({ "computador": nome_do_computador(), "versaoAtualizador": env!("CARGO_PKG_VERSION") })
}

pub struct Ativacao {
    pub codigo: String,
    pub url_autorizar: String,
    pub expira_s: u64,
    pub intervalo_s: u64,
}

pub fn iniciar_ativacao(api: &str) -> Result<Ativacao, String> {
    let r = pedir(&format!("{api}/extensao/dispositivos/ativacoes"), None, Some(&quem_sou().to_string()), LIMITE)?;
    if r.status == 404 {
        return Err("Esta plataforma ainda não distribui a extensão por aqui (a API não tem a ativação).".into());
    }
    if r.status != 200 {
        return Err(format!("A plataforma não aceitou o pedido de ativação (HTTP {}).", r.status));
    }
    let v: Value = serde_json::from_slice(&r.corpo).map_err(|_| "A plataforma respondeu algo ilegível.")?;
    let a = Ativacao {
        codigo: texto(&v, "codigo").ok_or("A plataforma não devolveu o código de ativação.")?,
        url_autorizar: texto(&v, "urlAutorizar").ok_or("A plataforma não devolveu a página de autorização.")?,
        expira_s: v.get("expiraEmSegundos").and_then(Value::as_u64).unwrap_or(600).clamp(30, 3600),
        intervalo_s: v.get("intervaloSegundos").and_then(Value::as_u64).unwrap_or(3).clamp(1, 60),
    };
    // Este endereço vai ser aberto no navegador: só https (ou a própria máquina, em ensaio).
    if !url_permitida(&a.url_autorizar) {
        return Err("A plataforma devolveu uma página de autorização inválida.".into());
    }
    Ok(a)
}

pub enum Situacao {
    Aguardando,
    Autorizado(String),
    Encerrada,
}

/// Troca o código de ativação pelo token do computador.
pub fn consultar_ativacao(api: &str, codigo: &str) -> Result<Situacao, String> {
    let mut corpo = quem_sou();
    corpo["codigo"] = json!(codigo);
    let r = pedir(&format!("{api}/extensao/dispositivos/ativacoes/token"), None, Some(&corpo.to_string()), LIMITE)?;
    match r.status {
        200 => serde_json::from_slice::<Value>(&r.corpo)
            .ok()
            .and_then(|v| texto(&v, "token"))
            .map(Situacao::Autorizado)
            .ok_or_else(|| "A plataforma autorizou, mas não devolveu o token.".to_string()),
        202 => Ok(Situacao::Aguardando),
        410 | 404 => Ok(Situacao::Encerrada),
        s => Err(format!("A plataforma respondeu HTTP {s} à consulta da ativação.")),
    }
}

#[cfg(test)]
mod testes {
    use super::*;

    #[test]
    fn o_que_a_pessoa_digita_vira_candidatos() {
        assert_eq!(
            candidatos(" plataforma.exemplo.gov.br/ "),
            ["https://plataforma.exemplo.gov.br", "https://api.plataforma.exemplo.gov.br"]
        );
        assert_eq!(candidatos("https://api.exemplo.gov.br"), ["https://api.exemplo.gov.br"]);
        assert_eq!(candidatos("api.exemplo.gov.br"), ["https://api.exemplo.gov.br"]);
        assert_eq!(candidatos("http://127.0.0.1:8765"), ["http://127.0.0.1:8765"]);
        // http na rede não passa; vazio também não
        assert!(candidatos("http://exemplo.gov.br").is_empty());
        assert!(candidatos("   ").is_empty());
    }
}
