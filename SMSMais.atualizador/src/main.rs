// SMSMais — Atualizador.
//
// Programa pequeno e genérico (igual para todo município) que fica residente no PC — abre sozinho
// a cada logon, sem administrador, com um ícone na bandeja — e:
//   1. mantém a EXTENSÃO do Chrome atualizada: pergunta à plataforma do município (com o token
//      deste computador) qual é a versão publicada, baixa o pacote e troca os arquivos da pasta
//      que o Chrome carrega ("Carregar sem compactação");
//   2. faz a extensão recarregar sozinha: a troca termina pelo manifest.json, e a extensão, que
//      vigia a versão do manifest no disco, chama chrome.runtime.reload();
//   3. mantém A SI MESMO atualizado, também pela plataforma;
//   4. avisa quando o Chrome está aberto sem a extensão (ou com ela desativada).
//
// Tudo fica hospedado na plataforma; nada vem de repositório público. O instalador é baixado
// pelo painel por quem está logado e já chega sabendo o endereço e com a autorização.
//
// Executável "de janela": não abre console preto no logon.
#![windows_subsystem = "windows"]

mod bandeja;
mod config;
mod dialogo;
mod extensao;
mod navegador;
mod plataforma;
mod proprio;
mod rede;
mod sistema;
mod versao;

use std::fs;
use std::path::Path;
use std::sync::atomic::{AtomicBool, Ordering};
use std::sync::Arc;
use std::thread::{self, sleep};
use std::time::{Duration, Instant};

use serde_json::json;

use config::Config;
use plataforma::Situacao;
use proprio::VERSAO;
use sistema::{caixa, dizer, imprimir, locais, registrar, Acordou, Eventos, Sinal, TITULO};

const AJUDA: &str = "\
SMSMais — Atualizador

  (sem opção)             duplo clique: instala; já instalado, fica residente
  --instalar              instala (não pede administrador) e inicia
      --pasta DIR         pasta da extensão (a que o Chrome carrega)
      --silencioso        sem janelas
  --configurar [ENDERECO] liga este computador à plataforma (autorização pelo painel)
      --sem-navegador     só mostra o endereço de autorização, sem abrir o navegador
  --agora                 pede ao residente para conferir agora (o Verificar agora do menu)
  --uma-vez               confere e atualiza agora, neste processo, e sai
  --status                mostra versões, pastas e a situação no Chrome
  --desinstalar           tira do início automático e encerra o residente
  --versao                versão deste executável";

const PRIMEIRA_VEZ: &str = "Só desta vez, no Chrome:\n\
     1) abra chrome://extensions\n\
     2) ligue o \"Modo do desenvolvedor\"\n\
     3) \"Carregar sem compactação\" e escolha a pasta acima\n\n\
     Daqui em diante a extensão se atualiza e se recarrega sozinha.";

fn main() {
    std::panic::set_hook(Box::new(|info| registrar(&format!("ERRO INTERNO: {info}"))));

    let args: Vec<String> = std::env::args().skip(1).collect();
    let tem = |opcao: &str| args.iter().any(|a| a == opcao);
    // Valor que vem depois da opção (se o que vem depois não for outra opção).
    let valor = |opcao: &str| {
        args.iter().position(|a| a == opcao).and_then(|i| args.get(i + 1)).filter(|v| !v.starts_with("--")).cloned()
    };

    let codigo = if tem("--versao") {
        imprimir(VERSAO);
        0
    } else if tem("--ajuda") || tem("-h") || tem("/?") {
        imprimir(AJUDA);
        0
    } else if tem("--desinstalar") {
        desinstalar();
        0
    } else if tem("--status") {
        status();
        0
    } else if tem("--uma-vez") {
        uma_vez()
    } else if tem("--agora") {
        // O mesmo que "Verificar agora" do menu, para script de suporte.
        if sistema::sinalizar(Sinal::Agora) {
            imprimir("Pedido entregue ao residente: ele confere agora.");
            0
        } else {
            imprimir("O residente não está rodando.");
            1
        }
    } else if tem("--residente") {
        residente(tem("--configurar"));
        0
    } else if tem("--instalar") {
        instalar(valor("--pasta"), tem("--silencioso"))
    } else if tem("--configurar") {
        let endereco = valor("--configurar").map(Endereco::Dado).unwrap_or(Endereco::Conhecido);
        configurar_pela_linha_de_comando(endereco, !tem("--sem-navegador"))
    } else if args.is_empty() {
        // Duplo clique. No lugar de instalação é o próprio residente; fora dele, instala.
        match std::env::current_exe() {
            Ok(este) if sistema::mesmo_arquivo(&este, &locais().exe) => {
                residente(false);
                0
            }
            _ => instalar(None, false),
        }
    } else {
        imprimir(&format!("Opção desconhecida: {}\n\n{AJUDA}", args.join(" ")));
        2
    };
    std::process::exit(codigo);
}

// ---------------------------------------------------------------------- instalar
/// Como este computador ficou em relação à plataforma depois de instalar.
enum Ligacao {
    /// O instalador veio do painel com a autorização: já está ligado (nome da instituição).
    Autorizado(String),
    JaEstava,
    /// Falta autorizar. `endereco_conhecido`: o instalador trouxe o endereço da plataforma.
    Falta { endereco_conhecido: bool },
}

fn instalar(pasta: Option<String>, silencioso: bool) -> i32 {
    match instalar_passos(pasta) {
        Ok(ligacao) => {
            let (situacao, falta) = match &ligacao {
                Ligacao::Autorizado(instituicao) => (
                    format!("Este computador foi autorizado em {instituicao}. A extensão será baixada agora e mantida em dia."),
                    false,
                ),
                Ligacao::JaEstava => ("Este computador já estava autorizado na plataforma.".to_string(), false),
                Ligacao::Falta { endereco_conhecido: true } => (
                    "Falta autorizar este computador: a seguir abre a página do painel — entre e clique em autorizar."
                        .to_string(),
                    true,
                ),
                Ligacao::Falta { endereco_conhecido: false } => (
                    "Falta ligar este computador à plataforma: a seguir, informe o endereço dela e autorize no painel."
                        .to_string(),
                    true,
                ),
            };
            let resumo = format!(
                "Atualizador v{VERSAO} instalado — não precisou de administrador.\n\
                 Ele abre sozinho a cada logon e fica no ícone \"+\" perto do relógio.\n\n{situacao}"
            );
            registrar(&format!("instalado em {}", locais().exe.display()));
            imprimir(&resumo);
            if !silencioso {
                caixa(&resumo, false);
            }
            // O Configurar abre no residente (é ele que tem o ícone e os avisos).
            let extras: &[&str] = if falta && !silencioso { &["--configurar"] } else { &[] };
            match proprio::iniciar_residente(extras) {
                Ok(()) => 0,
                Err(e) => {
                    dizer(&e);
                    1
                }
            }
        }
        Err(e) => {
            let msg = format!("A instalação não terminou: {e}");
            dizer(&msg);
            if !silencioso {
                caixa(&msg, true);
            }
            1
        }
    }
}

fn instalar_passos(pasta: Option<String>) -> Result<Ligacao, String> {
    let l = locais();
    let este = std::env::current_exe().map_err(|e| e.to_string())?;
    let bytes = fs::read(&este).map_err(|e| format!("não consegui ler {}: {e}", este.display()))?;
    fs::create_dir_all(&l.base).map_err(|e| format!("não consegui criar {}: {e}", l.base.display()))?;

    let pasta = match pasta {
        Some(p) => std::path::absolute(&p).map_err(|e| format!("pasta inválida \"{p}\": {e}"))?,
        None => Config::ler().pasta_extensao,
    };

    // O residente anterior (se houver) sai: solta o executável e dá a vez ao que vai nascer.
    sistema::sinalizar(Sinal::Parar);
    // Um instalador antigo não rebaixa o que já está instalado (outro usuário do PC pode ter
    // instalado, ou a auto-atualização já passou por aqui).
    let ja_mais_novo = proprio::versao_instalada().is_some_and(|v| versao::mais_nova(&v, VERSAO));
    if !sistema::mesmo_arquivo(&este, &l.exe) && !ja_mais_novo {
        gravar_executavel(proprio::sem_rabicho(&bytes), &l.exe)?;
    }

    Config::gravar(&[("pasta_extensao", json!(pasta.to_string_lossy()))])?;
    sistema::registrar_inicio_automatico(&l.exe)?;

    if Config::ler().origem().is_some() {
        return Ok(Ligacao::JaEstava);
    }
    // Instalador baixado pelo painel: traz o endereço da API e um código de uso único, emitido
    // para quem estava logado. Trocado pelo token, o computador já sai autorizado.
    let Some(embutido) = proprio::ler_embutido(&bytes).filter(|e| rede::url_permitida(&e.api)) else {
        return Ok(Ligacao::Falta { endereco_conhecido: Config::ler().plataforma.is_some() });
    };
    Config::gravar(&[("plataforma_api", json!(embutido.api))])?;
    match plataforma::consultar_ativacao(&embutido.api, &embutido.codigo) {
        Ok(Situacao::Autorizado(token)) => {
            guardar_autorizacao(&embutido.api, &token)?;
            let instituicao = plataforma::localizar(&embutido.api).map(|(_, nome)| nome).unwrap_or_else(|_| "plataforma".into());
            registrar(&format!("computador autorizado em {instituicao} ({}) pelo instalador", embutido.api));
            Ok(Ligacao::Autorizado(instituicao))
        }
        // Código vencido ou já usado (instalador antigo, copiado de outro PC): autoriza pelo painel.
        Ok(_) => Ok(Ligacao::Falta { endereco_conhecido: true }),
        Err(e) => {
            registrar(&format!("instalador: não consegui trocar o código de ativação ({e})"));
            Ok(Ligacao::Falta { endereco_conhecido: true })
        }
    }
}

fn guardar_autorizacao(api: &str, token: &str) -> Result<(), String> {
    let cifrado = sistema::proteger(token).ok_or("não consegui guardar o token no cofre do Windows")?;
    Config::gravar(&[("plataforma_api", json!(api)), ("token_protegido", json!(cifrado))])
}

/// Grava o executável no lugar de instalação. Se o de lá ainda estiver em uso (residente saindo),
/// espera; persistindo, renomeia o antigo para o lado — o Windows deixa renomear executável em
/// uso, só não deixa gravar por cima.
fn gravar_executavel(conteudo: &[u8], para: &Path) -> Result<(), String> {
    if fs::read(para).map(|atual| atual == conteudo).unwrap_or(false) {
        return Ok(()); // já é este (outro usuário do PC instalou a mesma versão)
    }
    let mut ultimo = String::new();
    for tentativa in 0..20 {
        match fs::write(para, conteudo) {
            Ok(()) => return Ok(()),
            Err(e) => ultimo = e.to_string(),
        }
        if tentativa == 10 {
            let lado = para.with_file_name("smsmais-atualizador.antigo.exe");
            let _ = fs::remove_file(&lado);
            let _ = fs::rename(para, &lado);
        }
        sleep(Duration::from_millis(300));
    }
    Err(format!("não consegui gravar o atualizador em {}: {ultimo}", para.display()))
}

// -------------------------------------------------------------------- configurar
/// De onde vem o endereço da plataforma no Configurar.
enum Endereco {
    /// Pergunta numa janela (mostrando o atual).
    Perguntar,
    Dado(String),
    /// Usa o que já está configurado; sem nenhum, pergunta.
    Conhecido,
}

/// Liga este computador à plataforma: endereço da API → código de ativação → alguém logado no
/// painel autoriza → o token deste computador é guardado (cifrado pelo Windows).
/// `Ok(None)` = a pessoa cancelou. `falar` leva o andamento para quem está olhando.
fn configurar(endereco: Endereco, abrir_navegador: bool, falar: &dyn Fn(&str)) -> Result<Option<String>, String> {
    let atual = Config::ler().plataforma;
    let digitado = match (endereco, atual) {
        (Endereco::Dado(e), _) => e,
        (Endereco::Conhecido, Some(a)) => a,
        (_, atual) => {
            let pergunta = "Endereço da plataforma do seu município\n(o mesmo em que você entra no painel):";
            match dialogo::perguntar(TITULO, pergunta, &atual.unwrap_or_default()) {
                Some(d) => d,
                None => return Ok(None),
            }
        }
    };
    let (api, instituicao) = plataforma::localizar(&digitado)?;
    let ativacao = plataforma::iniciar_ativacao(&api)?;
    if abrir_navegador {
        sistema::abrir(&ativacao.url_autorizar);
        falar(&format!("{instituicao}: autorize este computador na página que abriu no navegador."));
    } else {
        falar(&format!(
            "{instituicao}: abra este endereço num navegador logado na plataforma e autorize este computador:\n{}",
            ativacao.url_autorizar
        ));
    }

    let limite = Instant::now() + Duration::from_secs(ativacao.expira_s);
    let mut ultimo_erro = String::new();
    while Instant::now() < limite {
        sleep(Duration::from_secs(ativacao.intervalo_s));
        match plataforma::consultar_ativacao(&api, &ativacao.codigo) {
            Ok(Situacao::Autorizado(token)) => {
                guardar_autorizacao(&api, &token)?;
                registrar(&format!("computador autorizado em {instituicao} ({api})"));
                sistema::sinalizar(Sinal::Agora);
                return Ok(Some(format!("Computador autorizado em {instituicao}. A extensão será mantida em dia.")));
            }
            Ok(Situacao::Aguardando) => {}
            Ok(Situacao::Encerrada) => {
                return Err("A autorização foi recusada ou venceu. Use o Configurar de novo.".into());
            }
            Err(e) => ultimo_erro = e, // rede oscilando: segue tentando até o prazo
        }
    }
    Err(format!(
        "Ninguém autorizou este computador a tempo. Use o Configurar de novo.{}",
        if ultimo_erro.is_empty() { String::new() } else { format!(" ({ultimo_erro})") }
    ))
}

fn configurar_pela_linha_de_comando(endereco: Endereco, abrir_navegador: bool) -> i32 {
    match configurar(endereco, abrir_navegador, &|m| dizer(m)) {
        Ok(Some(msg)) => {
            dizer(&msg);
            0
        }
        Ok(None) => {
            imprimir("Cancelado.");
            1
        }
        Err(e) => {
            dizer(&format!("Configurar: {e}"));
            1
        }
    }
}

/// O Configurar do residente: numa thread só dele, com o andamento saindo pelo ícone. Um de cada
/// vez. `perguntar` = veio do menu (mostra a janela do endereço); sem ele, é o seguimento da
/// instalação e usa o endereço que o instalador trouxe.
fn configurar_pelo_icone(perguntar: bool) {
    static EM_CURSO: AtomicBool = AtomicBool::new(false);
    if EM_CURSO.swap(true, Ordering::SeqCst) {
        return;
    }
    thread::spawn(move || {
        let endereco = if perguntar { Endereco::Perguntar } else { Endereco::Conhecido };
        match configurar(endereco, true, &|m| bandeja::avisar(TITULO, m)) {
            Ok(Some(msg)) => bandeja::avisar(TITULO, &msg),
            Ok(None) => {}
            Err(e) => {
                registrar(&format!("configurar: {e}"));
                caixa(&e, true);
            }
        }
        EM_CURSO.store(false, Ordering::SeqCst);
    });
}

// --------------------------------------------------------------------- residente
/// Guarda o último erro de cada frente para não encher o log com a mesma linha a cada ciclo
/// (PC sem internet por horas = uma linha, não cem).
struct Frente {
    nome: &'static str,
    ultimo_erro: String,
}

impl Frente {
    fn nova(nome: &'static str) -> Frente {
        Frente { nome, ultimo_erro: String::new() }
    }
    /// `true` = erro novo (diferente do último).
    fn erro(&mut self, e: String) -> bool {
        if e == self.ultimo_erro {
            return false;
        }
        registrar(&format!("{}: {e}", self.nome));
        self.ultimo_erro = e;
        true
    }
    fn ok(&mut self) {
        if !self.ultimo_erro.is_empty() {
            registrar(&format!("{}: voltou ao normal", self.nome));
            self.ultimo_erro.clear();
        }
    }
}

fn residente(configurar_ao_abrir: bool) {
    // Espera a vez: na auto-atualização e na reinstalação o residente novo nasce com o antigo
    // ainda saindo. Se depois disso ainda há outro vivo, este não é necessário.
    let Some(_instancia) = sistema::instancia_unica(15_000) else {
        return;
    };
    let Some(eventos) = Eventos::criar() else {
        registrar("não consegui criar os eventos do residente; encerrando");
        return;
    };
    let eventos = Arc::new(eventos);
    proprio::limpar_restos();
    registrar(&format!("residente iniciado (extensão em {})", Config::ler().pasta_extensao.display()));

    let trabalhador = {
        let eventos = Arc::clone(&eventos);
        thread::spawn(move || {
            ciclos(&eventos);
            bandeja::encerrar();
        })
    };
    if configurar_ao_abrir {
        configurar_pelo_icone(false);
    }
    // Fica aqui (laço de mensagens do ícone) até a thread dos ciclos terminar.
    bandeja::rodar(bandeja::Acoes {
        agora: || {
            sistema::sinalizar(Sinal::Agora);
        },
        pasta: || {
            let pasta = Config::ler().pasta_extensao;
            let _ = fs::create_dir_all(&pasta);
            sistema::abrir(&pasta.to_string_lossy());
        },
        registro: || sistema::abrir(&locais().log.to_string_lossy()),
        configurar: || configurar_pelo_icone(true),
        sair: || {
            sistema::sinalizar(Sinal::Parar);
        },
    });
    let _ = trabalhador.join();
}

fn mostrar_situacao(cfg: &Config, falhou: bool, chrome: Option<navegador::Situacao>) {
    let local = extensao::versao_local(&cfg.pasta_extensao);
    let (dica, linha) = if let Some(aviso) = chrome.and_then(|c| c.aviso()) {
        (format!("{TITULO} v{VERSAO}\n{aviso}"), format!("{aviso} — veja chrome://extensions"))
    } else if cfg.origem().is_none() {
        (
            format!("{TITULO}\nFalta configurar: clique e escolha Configurar"),
            "Extensão: falta configurar este computador".to_string(),
        )
    } else {
        let versao = local.map(|v| format!("v{v}")).unwrap_or_else(|| "ainda não baixada".into());
        let canal = config::ler_estado().canal.map(|c| format!(" · canal {c}")).unwrap_or_default();
        let hora = sistema::hora();
        if falhou {
            (
                format!("{TITULO} v{VERSAO}\nExtensão {versao} · não consegui conferir às {hora}"),
                format!("Extensão {versao}{canal} · falha ao conferir às {hora}"),
            )
        } else {
            (
                format!("{TITULO} v{VERSAO}\nExtensão {versao} em dia · conferida às {hora}"),
                format!("Extensão {versao}{canal} · conferida às {hora}"),
            )
        }
    };
    bandeja::situacao(&dica, &linha);
}

/// A situação da extensão no Chrome deste usuário, e o rótulo que vai para a plataforma (o
/// inventário do painel). Só há o que AVISAR com o Chrome aberto e a extensão já baixada.
fn olhar_o_chrome(cfg: &Config) -> (Option<navegador::Situacao>, &'static str) {
    let nos_perfis = navegador::situacao(&cfg.dados_do_chrome, &cfg.pasta_extensao);
    if !navegador::chrome_aberto() {
        return (None, "fechado");
    }
    match nos_perfis {
        None => (None, "sem-perfil"),
        Some(s) if extensao::versao_local(&cfg.pasta_extensao).is_none() => (None, s.rotulo()),
        Some(s) => (Some(s), s.rotulo()),
    }
}

fn avisar_do_chrome(situacao: navegador::Situacao, cfg: &Config) {
    let Some(titulo) = situacao.aviso() else {
        return;
    };
    let como = match situacao {
        navegador::Situacao::NaoCarregada => format!(
            "Em chrome://extensions, use \"Carregar sem compactação\" e escolha a pasta {}.",
            cfg.pasta_extensao.display()
        ),
        navegador::Situacao::Desativada => "Reative a extensão em chrome://extensions.".to_string(),
        _ => "Ligue o \"Modo do desenvolvedor\" em chrome://extensions — sem ele o Chrome desativa a extensão."
            .to_string(),
    };
    registrar(&format!("chrome: {titulo}"));
    bandeja::avisar(titulo, &como);
}

fn ciclos(eventos: &Eventos) {
    mostrar_situacao(&Config::ler(), false, None);
    // Logo depois de ligar o PC a rede pode ainda não ter subido.
    if sistema::milissegundos_ligado() < 3 * 60 * 1000 {
        if let Acordou::Parar = eventos.esperar(30_000) {
            return;
        }
    }

    let mut f_proprio = Frente::nova("atualizador");
    let mut f_extensao = Frente::nova("extensão");
    let mut proxima_do_proprio = Instant::now();
    let mut chrome_anterior: Option<navegador::Situacao> = None;

    loop {
        let cfg = Config::ler();
        proprio::limpar_antigo();

        // O atualizador muda pouco: confere na partida, de 6 em 6 horas e no "Verificar agora".
        if Instant::now() >= proxima_do_proprio {
            proxima_do_proprio = Instant::now() + Duration::from_secs(6 * 3600);
            match proprio::conferir(&cfg) {
                Ok(Some(v)) => {
                    registrar(&format!("atualizador trocado pela v{v}; renascendo do executável novo"));
                    match proprio::iniciar_residente(&[]) {
                        Ok(()) => return, // o novo espera este sair e assume
                        Err(e) => {
                            f_proprio.erro(e);
                        }
                    }
                }
                Ok(None) => f_proprio.ok(),
                Err(e) => {
                    f_proprio.erro(e);
                    proxima_do_proprio = Instant::now() + Duration::from_secs(3600);
                }
            }
        }

        let tinha = extensao::versao_local(&cfg.pasta_extensao).is_some();
        let (chrome, rotulo) = olhar_o_chrome(&cfg);
        let falhou = match extensao::conferir(&cfg, rotulo) {
            Ok(Some(v)) => {
                f_extensao.ok();
                registrar(&format!("extensão atualizada para a v{v} em {}", cfg.pasta_extensao.display()));
                if tinha {
                    bandeja::avisar(
                        &format!("Extensão atualizada — v{v}"),
                        "Ela se recarrega sozinha. Recarregue (F5) as páginas abertas que usam a extensão.",
                    );
                } else {
                    // Primeira carga neste PC: falta apontar o Chrome para a pasta (uma vez só).
                    let pasta = cfg.pasta_extensao.display().to_string();
                    thread::spawn(move || {
                        caixa(&format!("Extensão v{v} baixada em:\n{pasta}\n\n{PRIMEIRA_VEZ}"), false);
                        sistema::abrir(&pasta);
                    });
                }
                false
            }
            Ok(None) => {
                f_extensao.ok();
                false
            }
            Err(e) => {
                let recusado = e.starts_with(plataforma::RECUSADO);
                if f_extensao.erro(e.clone()) && recusado {
                    bandeja::avisar(TITULO, &e);
                }
                true
            }
        };

        // Na primeira carga a janela de instruções já diz o que fazer no Chrome; depois disso, o
        // aviso sai quando a situação MUDA (não a cada ciclo).
        let chrome = if tinha { chrome } else { None };
        if chrome != chrome_anterior {
            match chrome {
                Some(s) if s.aviso().is_some() => avisar_do_chrome(s, &cfg),
                _ if chrome_anterior.is_some_and(|a| a.aviso().is_some()) => registrar("chrome: extensão em ordem"),
                _ => {}
            }
            chrome_anterior = chrome;
        }
        mostrar_situacao(&cfg, falhou, chrome);

        let espera_ms = cfg.intervalo_min.saturating_mul(60_000).saturating_add(sistema::sorteio(60_000));
        match eventos.esperar(espera_ms.min(u32::MAX as u64 - 1) as u32) {
            Acordou::Parar => {
                registrar("residente encerrado a pedido");
                return;
            }
            Acordou::Agora => proxima_do_proprio = Instant::now(),
            Acordou::Tempo => {}
        }
    }
}

// ---------------------------------------------------------------------- uma vez
fn uma_vez() -> i32 {
    let cfg = Config::ler();
    if cfg.origem().is_none() {
        dizer("Este computador ainda não foi configurado (use --configurar).");
        return 1;
    }
    let mut codigo = 0;

    match proprio::conferir(&cfg) {
        Ok(Some(v)) => dizer(&format!("Atualizador trocado pela v{v} (vale a partir da próxima execução).")),
        Ok(None) => dizer(&format!("Atualizador em dia (v{VERSAO}).")),
        Err(e) => {
            dizer(&format!("Atualizador: {e}"));
            codigo = 1;
        }
    }
    match extensao::conferir(&cfg, olhar_o_chrome(&cfg).1) {
        Ok(Some(v)) => dizer(&format!("Extensão atualizada para a v{v} em {}.", cfg.pasta_extensao.display())),
        Ok(None) => dizer(&format!(
            "Extensão em dia (v{}).",
            extensao::versao_local(&cfg.pasta_extensao).unwrap_or_else(|| "?".into())
        )),
        Err(e) => {
            dizer(&format!("Extensão: {e}"));
            codigo = 1;
        }
    }
    codigo
}

// ------------------------------------------------------------------------ status
fn status() {
    let l = locais();
    let cfg = Config::ler();
    let sim_nao = |b: bool| if b { "sim" } else { "não" };
    imprimir(&format!(
        "Atualizador v{VERSAO}\n\
         Instalado em ........: {}{}\n\
         Início automático ...: {}\n\
         Residente rodando ...: {}\n\
         Plataforma ..........: {}\n\
         Computador autorizado: {}\n\
         Canal (na plataforma): {}\n\
         Pasta da extensão ...: {}\n\
         Extensão no disco ...: {}\n\
         Extensão no Chrome ..: {}\n\
         Registro ............: {}",
        l.exe.display(),
        if l.exe.is_file() { "" } else { "  (não instalado)" },
        sistema::inicio_automatico().unwrap_or_else(|| "não".into()),
        sim_nao(sistema::residente_vivo()),
        cfg.plataforma.as_deref().unwrap_or("não configurada"),
        sim_nao(cfg.token.is_some()),
        config::ler_estado().canal.unwrap_or_else(|| "ainda não informado".into()),
        cfg.pasta_extensao.display(),
        extensao::versao_local(&cfg.pasta_extensao).map(|v| format!("v{v}")).unwrap_or_else(|| "nenhuma".into()),
        match navegador::situacao(&cfg.dados_do_chrome, &cfg.pasta_extensao) {
            None => "o Chrome nunca foi usado por este usuário".to_string(),
            Some(s) => format!(
                "{} (Chrome {})",
                s.aviso().unwrap_or("carregada e ativa"),
                if navegador::chrome_aberto() { "aberto" } else { "fechado" }
            ),
        },
        l.log.display(),
    ));
}

// ------------------------------------------------------------------- desinstalar
fn desinstalar() {
    let l = locais();
    sistema::remover_inicio_automatico();
    let havia = sistema::sinalizar(Sinal::Parar);

    // Rodando de fora da pasta de instalação dá para apagar tudo; de dentro, o Windows não deixa
    // o executável apagar a si mesmo — fica só o arquivo, sem nada que o execute.
    let de_fora = std::env::current_exe().map(|e| !sistema::mesmo_arquivo(&e, &l.exe)).unwrap_or(false);
    let mut apagou = false;
    if de_fora {
        for _ in 0..20 {
            if fs::remove_dir_all(&l.base).is_ok() || !l.base.exists() {
                apagou = true;
                break;
            }
            sleep(Duration::from_millis(300));
        }
    }
    imprimir(&format!(
        "Início automático removido.{}{}\nA pasta da extensão não foi tocada.",
        if havia { " Residente encerrado." } else { "" },
        if apagou {
            format!(" Pasta {} apagada.", l.base.display())
        } else {
            format!(" Para terminar, apague a pasta {}.", l.base.display())
        },
    ));
}
