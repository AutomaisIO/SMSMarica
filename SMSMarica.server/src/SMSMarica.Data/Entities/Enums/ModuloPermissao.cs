namespace SMSMarica.Data.Entities.Enums;

/// <summary>
/// Módulos do sistema que aceitam controle de acesso. O valor inteiro
/// é estável (persistido) — não renumerar valores existentes.
/// </summary>
public enum ModuloPermissao
{
    Pacientes = 1,
    Unidades = 2,
    Veiculos = 3,
    Motoristas = 4,
    Usuarios = 5,
    TiposTratamento = 6,
    Perfis = 7,
    Tratamentos = 8,
    Translados = 9,
    Rastreamento = 10,
    Avaliacoes = 11,
    Pacs = 12,
    Medicos = 13,
    Laudos = 14,
    LaudosTemplates = 15,
    SolicitacoesExame = 16,
    TiposExame = 17,
    ProcedimentosSigtap = 18,

    /// <summary>Usar o assistente de IA (fazer perguntas em linguagem natural).</summary>
    Inteligencia = 19,

    /// <summary>Configurar o módulo IA: token do provedor e bases de dados.</summary>
    InteligenciaConfiguracao = 20,

    /// <summary>Governar o aprendizado da IA: ver o log de melhorias e remover instruções aprendidas.</summary>
    InteligenciaAprendizado = 21,

    /// <summary>Especialidades médicas (tabela de referência do agendamento).</summary>
    Especialidades = 22,

    /// <summary>Agendas dos médicos e marcação de consultas (agendamento local).</summary>
    Agendamentos = 23,

    /// <summary>Consultar o feed de leitura do SISREG (solicitações/marcações).</summary>
    Sisreg = 24,

    /// <summary>Configurar a integração SISREG: credenciais, UF/município, centrais reguladoras.</summary>
    SisregConfiguracao = 25,

    /// <summary>Equipamentos das unidades (recurso agendável para exames de imagem).</summary>
    Equipamentos = 26,

    /// <summary>Sincronização/importação de prontuários de PEPs (Salux e futuros) para o hub FHIR.</summary>
    SincronizacaoPep = 27,

    /// <summary>Gerenciar tokens de API (chaves de serviço para integrações externas, ex.: CentralIA).</summary>
    ApiTokens = 28,

    /// <summary>Portal do cidadão/paciente (PWA): acesso ao próprio escopo (translados, acompanhante).</summary>
    Cidadao = 29,

    /// <summary>Configurar integrações externas do TFD (Google Maps, WhatsApp/Meta).</summary>
    IntegracoesConfig = 30,

    /// <summary>Faturamento SUS / geração de BPA do transporte (TFD).</summary>
    Faturamento = 31,

    /// <summary>Configurar o cabeçalho/rodapé institucional global dos laudos (PDF).</summary>
    ConfiguracaoLaudo = 32,

    /// <summary>Consultar a trilha de auditoria do sistema (somente leitura).</summary>
    Auditoria = 33,

    /// <summary>Consultar o log de erros não tratados do sistema (diagnóstico, só leitura).</summary>
    Erros = 34,

    /// <summary>Central de Atendimento (chat WhatsApp): ver/atender conversas da(s) própria(s) unidade(s).</summary>
    Conversas = 35,

    /// <summary>Supervisão do chat: ver TODAS as conversas (qualquer unidade), reatribuir de terceiros
    /// e encaminhar entre unidades.</summary>
    ConversasSupervisao = 36,

    /// <summary>Gestão de tickets de suporte: ver TODOS os tickets, responder, mudar status e
    /// configurar a visibilidade. Abrir/ver os próprios tickets não exige este módulo (qualquer
    /// usuário autenticado pode).</summary>
    Ticket = 37,

    /// <summary>Gestão das notificações WhatsApp de agendamento (confirmação de exames):
    /// acompanhar envios/entregas/falhas, respostas do paciente e reenviar.</summary>
    NotificacoesAgendamento = 38,

    /// <summary>Sandbox de QA (admin): criar pacientes de teste, semear exames/agendamentos,
    /// gerar magic link e enviar mensagens de teste por WhatsApp (texto livre, janela 24h).</summary>
    Sandbox = 39,

    /// <summary>Cadastro das mensagens prontas ("respostas rápidas") do chat: criar, editar e
    /// definir as variáveis. USAR os atalhos no chat exige só <see cref="Conversas"/>.</summary>
    RespostasRapidas = 40,

    /// <summary>Consultas reguladas (SISREG): listar/consultar as solicitações de consulta
    /// (categoria não-imagem). Sem PACS/laudo. Ver ADR-0021.</summary>
    Consultas = 41,

    /// <summary>Mapeamento SIGTAP→TipoExame: curadoria dos exames de imagem importados sem tipo
    /// (pendentes) — criar/vincular TipoExame com backfill por código SIGTAP.</summary>
    MapeamentoSigtap = 42,

    /// <summary>Agente IA: terminal do agente que roda NO SERVIDOR, com shell, banco de produção
    /// e o clone do repositório. Pode alterar código e commitar. Conceder apenas a quem já teria
    /// acesso administrativo ao servidor — não é um assistente de consulta como
    /// <see cref="Inteligencia"/>.</summary>
    AgenteIa = 43,

    /// <summary>Indicadores contratuais do HMCML: consultar os painéis por aba da planilha.
    /// A ação de Edição libera alterar o SQL do motor de cada indicador (consulta read-only
    /// contra a base de origem) — conceder só a quem for aprimorar o cálculo.</summary>
    Indicadores = 44,

    /// <summary>Estatísticas de atendimento (retrato do WhatsApp): dashboard gerencial com
    /// contagens de mensagens (templates do sistema x atendente, sessão, recebidas), séries
    /// diárias, status de entrega e ranking de atendentes. Visão global (só leitura).</summary>
    Estatistica = 45,

    /// <summary>Modo desenvolvedor da Consulta Inteligente: libera o checkbox que mostra o
    /// raciocínio completo do motor (consultas SQL, ferramentas, passos) em vez de só o
    /// resultado concreto. Puramente de UI — não abre nenhum acesso novo a dado. Conceder a
    /// quem precisa depurar/entender as consultas.</summary>
    InteligenciaConsultaDev = 46,

    // ---- Processo Regulatório (ADR-0024) ----
    // Cinco módulos e não um, porque AcoesPermissao só tem 4 flags: não há como separar "dar
    // parecer" de "registrar contato" dentro do mesmo módulo. Mesmo idioma de
    // Conversas/ConversasSupervisao e Sisreg/SisregConfiguracao.

    /// <summary>Regulação — lado da UNIDADE SOLICITANTE (UBS/unidade especializada): abrir processo,
    /// anexar documento, enviar, responder pendência, acompanhar e conversar. Escopado por unidade:
    /// o usuário só vê os processos das unidades a que está vinculado.</summary>
    Regulacao = 47,

    /// <summary>Regulação — TRIAGEM TÉCNICA (visão global do município): assumir da fila, conferir
    /// dados e anexos, pedir complementação, encaminhar ao médico, alterar prioridade. A Exclusão
    /// libera cancelar por duplicidade, rejeitar administrativamente e reabrir processo encerrado.</summary>
    RegulacaoTriagem = 48,

    /// <summary>Regulação — DECISÃO CLÍNICA do médico regulador (visão global): dar parecer, deferir,
    /// alterar prioridade/destino/procedimento e ver os documentos marcados como sensíveis. A
    /// Exclusão libera INDEFERIR. Exige, além da permissão, vínculo com um Practitioner.</summary>
    RegulacaoMedica = 49,

    /// <summary>Regulação — AGENDAMENTO (visão global): assumir da fila de deferidos, registrar o
    /// protocolo do sistema de destino (inclusive o nº SISREG marcado à mão), lançar o retorno com
    /// data/hora/local, reagendar, registrar atendimento e concluir.</summary>
    RegulacaoAgendamento = 50,

    /// <summary>Regulação — CONFIGURAÇÃO: prazos por etapa, validade do deferimento e
    /// obrigatoriedades de abertura.</summary>
    RegulacaoConfiguracao = 51,

    /// <summary>SISREG — MAPEAMENTO: a "verdade" da unidade no SISREG (profissionais executantes e
    /// seus procedimentos), com habilita/desabilita que define o que entra na varredura de agenda,
    /// e a credencial de operador do SISREG daquela unidade.</summary>
    SisregMapeamento = 52,

    /// <summary>Abrir SOLICITAÇÃO DE EXAME MANUAL (o botão "Nova solicitação" da tela de exames /
    /// <c>POST /solicitacoes-exame</c>). Separado de <see cref="SolicitacoesExame"/> porque criar
    /// à mão é a exceção — o fluxo normal entra pela importação do SISREG (serviço, sem passar por
    /// este gate) — e queremos liberar o botão só a usuários específicos, não a todo perfil com
    /// Inclusão no módulo. Usa apenas a ação <c>Inclusao</c> (ticket #89).</summary>
    SolicitacaoExameManual = 53,

    /// <summary>Regulação — **SER** (Sistema Estadual de Regulação, SES-RJ): a fila do Estado
    /// espelhada na nossa base (ADR-0042), com o histórico de cada solicitação. É **só leitura
    /// por natureza** — nada aqui escreve no SER —, então usa apenas <c>Consulta</c>. Separado de
    /// <see cref="RegulacaoAgendamento"/> porque é outra fonte, com outro vocabulário de situação
    /// e outra operação (acompanhar fila estadual, não marcar).</summary>
    RegulacaoSer = 54,

    /// <summary>Correção de IDENTIDADE de exame — trocar de qual paciente é um estudo já no PACS.
    /// Nasce do incidente de 11/08/2026 (a técnica puxou o item de worklist errado e as imagens de
    /// uma paciente foram arquivadas sob outro).
    /// <para>Módulo próprio, e não uma ação de <see cref="Pacs"/>, porque a operação é de outra
    /// natureza: <b>reescreve o objeto DICOM no PACS</b> (apaga o original e re-armazena com UIDs
    /// novos), descarta rascunhos de laudo e revoga o link já enviado ao paciente. Quem cuida do
    /// dia a dia de exames não precisa disso; quem precisa é um punhado de nomes. Mesmo caminho de
    /// <see cref="SolicitacaoExameManual"/> e <see cref="Sandbox"/>.</para>
    /// <para>Usa apenas <c>Edicao</c>: é ela que faz aparecer o card no pedido e libera a
    /// correção. Não há fila nem triagem — quem tem a permissão resolve na hora, na própria tela
    /// da solicitação; quem não tem, não vê o card.</para></summary>
    CorrecaoIdentidadeExame = 55,
}
