using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.RoboAtendimento.Runtime;

/// <summary>Uma ferramenta exposta ao modelo na Messages API: nome, descrição e schema dos
/// argumentos. <paramref name="InputSchema"/> é um objeto anônimo que vira JSON Schema na
/// serialização (mesmo estilo do <c>DistribuidorIa.Schema()</c>).</summary>
public sealed record RoboFerramenta(string Nome, string Descricao, object InputSchema);

/// <summary>
/// Definição das <c>tools</c> do robô para a Messages API — o que antes vivia só no Python
/// (<c>SMSMais.aiengine/atendimento_tool.py:CATALOGO</c>). Fica separado de
/// <see cref="ComandoRoboCatalogo"/> de propósito: aquele é o catálogo de TELA (rótulo curto para
/// ligar/desligar), este é o contrato com o MODELO — descrição longa, com o fluxo de duas fases e
/// as travas de identidade aprendidas no incidente de 26/08.
/// </summary>
public static class RoboFerramentaCatalogo
{
    public const string NomeResponderCidadao = "responder_cidadao";

    private static object Vazio() => new { type = "object", properties = new { }, required = Array.Empty<string>() };

    /// <summary>Ferramenta TERMINAL: único canal de saída. Nunca é opcional e não aparece na tela.
    /// Se o modelo terminar sem chamá-la, o motor descarta a prosa e devolve mensagem segura.</summary>
    public static RoboFerramenta ResponderCidadao { get; } = new(
        NomeResponderCidadao,
        "Entrega a resposta final ao cidadão. É o ÚNICO canal de saída: nada que você escrever fora "
        + "desta ferramenta chega à pessoa. Chame-a exatamente uma vez, por último.",
        new
        {
            type = "object",
            properties = new
            {
                texto = new { type = "string", description = "A mensagem que o cidadão vai ler. Só a mensagem — nunca seu raciocínio, planos ou nomes de ferramentas." },
                handoff = new { type = "boolean", description = "true quando a conversa deve ir para um atendente humano." },
                motivoHandoff = new { type = "string", description = "Motivo curto do hand-off (interno, o cidadão não vê)." },
                confianca = new { type = "number", description = "De 0 a 1: o quanto você confia nesta resposta." },
            },
            required = new[] { "texto" },
        });

    /// <summary>Ferramentas dos comandos, por enum. Só entram na sessão as habilitadas no assunto.</summary>
    public static IReadOnlyDictionary<ComandoRobo, RoboFerramenta> PorComando { get; } =
        new Dictionary<ComandoRobo, RoboFerramenta>
        {
            [ComandoRobo.RegistrarNumeroErrado] = new(
                "registrar_numero_errado",
                "Registra que este número NÃO é do paciente (\"não sou essa pessoa\"). Não corrige "
                + "cadastro nenhum: apenas abre uma pendência para a recepção resolver depois.",
                new
                {
                    type = "object",
                    properties = new
                    {
                        vinculo = new
                        {
                            type = "string",
                            @enum = new[] { "Parente", "Responsavel", "SemVinculo", "NaoInformado" },
                            description = "Vínculo de quem escreve com o paciente, se a pessoa disser.",
                        },
                        observacao = new { type = "string", description = "O que a pessoa relatou, em poucas palavras." },
                    },
                    required = new[] { "vinculo" },
                }),

            [ComandoRobo.EncaminharParaHumano] = new(
                "encaminhar_para_humano",
                "Devolve a conversa para um atendente humano. Use APENAS quando a pessoa pedir um "
                + "atendente ou quando o pedido estiver claramente fora do que você pode tratar.",
                Vazio()),

            [ComandoRobo.InformarHorarioAtendimento] = new(
                "informar_horario_atendimento",
                "Devolve o horário de atendimento para você informar ao cidadão.",
                Vazio()),

            [ComandoRobo.ConfirmarPresenca] = new(
                "confirmar_presenca",
                "Confirma a presença do paciente no agendamento. FLUXO EM DUAS CHAMADAS: primeiro "
                + "chame com cpf (os 4 PRIMEIROS dígitos — NUNCA peça o CPF completo) + mesNascimento "
                + "+ anoNascimento; o comando valida e devolve o NOME para você confirmar com a "
                + "pessoa. Só depois que ela confirmar o nome, chame de novo com confirmado=true. "
                + "Nunca revele procedimento, data, hora ou local antes de a identidade conferir.",
                GateComConfirmacao(comMotivo: false)),

            [ComandoRobo.IniciarCancelamento] = new(
                "iniciar_cancelamento",
                "Registra que o paciente NÃO vai comparecer (a equipe decide o cancelamento). MESMO "
                + "FLUXO EM DUAS CHAMADAS do confirmar_presenca: valida identidade, confirma o nome "
                + "com a pessoa e só então chame com confirmado=true.",
                GateComConfirmacao(comMotivo: true)),

            [ComandoRobo.ConsultarStatusExameRecente] = new(
                "consultar_status_exame_recente",
                "Consulta a situação do exame/laudo recente do paciente. Exige identidade: peça os 3 "
                + "PRIMEIROS dígitos do CPF (nunca o CPF completo) antes de chamar.",
                SoCpf()),

            [ComandoRobo.ConsultarPosicaoRegulacao] = new(
                "consultar_posicao_regulacao",
                "Situação do pedido na regulação (SER/SISREG/SERNIT). Devolve dado MINIMIZADO — em "
                + "regra só \"em fila\". Se o número não for verificado, o comando pede identidade: "
                + "colete os 4 primeiros dígitos do CPF e o mês/ano de nascimento e chame de novo.",
                new
                {
                    type = "object",
                    properties = new
                    {
                        cpf = new { type = "string", description = "Os 4 primeiros dígitos do CPF informados pela pessoa." },
                        mesNascimento = new { type = "integer", description = "Mês de nascimento (1 a 12)." },
                        anoNascimento = new { type = "integer", description = "Ano de nascimento com 4 dígitos." },
                    },
                    required = Array.Empty<string>(),
                }),

            [ComandoRobo.ConsultarCadastro] = new(
                "consultar_cadastro",
                "Confere a identidade da pessoa contra o cadastro e devolve o NOME dela. Peça os 4 "
                + "PRIMEIROS dígitos do CPF (nunca o CPF completo) e, em seguida, o mês e o ano de "
                + "nascimento — do PACIENTE do agendamento, não de quem escreve. Use SEMPRE esta "
                + "ferramenta para confirmar identidade: nunca diga por conta própria que os dados "
                + "conferem ou não conferem.",
                new
                {
                    type = "object",
                    properties = new
                    {
                        cpf = new { type = "string", description = "Os 4 PRIMEIROS dígitos do CPF do paciente." },
                        mesNascimento = new { type = "integer", description = "Mês de nascimento (1 a 12)." },
                        anoNascimento = new { type = "integer", description = "Ano de nascimento com 4 dígitos." },
                    },
                    required = new[] { "cpf", "mesNascimento", "anoNascimento" },
                }),

            [ComandoRobo.ConsultarUnidades] = new(
                "consultar_unidades",
                "Lista unidades de saúde da rede com nome e endereço — use para dizer ONDE fica um "
                + "posto, NUNCA invente endereço. Aceita 'termo' (nome da unidade ou bairro). "
                + "ATENÇÃO: isto NÃO é oferta de atendimento. Não marque consulta, não diga que a "
                + "unidade vai atender, não prometa horário nem encaixe — oriente sempre a procurar "
                + "o posto de saúde onde a pessoa já é atendida.",
                new
                {
                    type = "object",
                    properties = new
                    {
                        termo = new { type = "string", description = "Nome da unidade ou bairro. Vazio devolve uma amostra." },
                    },
                    required = Array.Empty<string>(),
                }),

            [ComandoRobo.VerificarCadastro] = new(
                "verificar_cadastro",
                "Valida os primeiros dígitos do CPF em resposta ao desafio cadastral. Confere → "
                + "libera o envio da confirmação do agendamento.",
                SoCpf()),
        };

    /// <summary>Nome da tool → comando (para resolver o <c>tool_use</c> que o modelo devolver).</summary>
    public static IReadOnlyDictionary<string, ComandoRobo> PorNome { get; } =
        PorComando.ToDictionary(kv => kv.Value.Nome, kv => kv.Key, StringComparer.OrdinalIgnoreCase);

    private static object SoCpf() => new
    {
        type = "object",
        properties = new
        {
            cpf = new { type = "string", description = "Os dígitos do CPF que a pessoa enviou (4 ou mais)." },
        },
        required = new[] { "cpf" },
    };

    private static object GateComConfirmacao(bool comMotivo) => comMotivo
        ? new
        {
            type = "object",
            properties = new
            {
                cpf = new { type = "string", description = "Os 4 PRIMEIROS dígitos do CPF. Nunca peça o CPF completo." },
                mesNascimento = new { type = "integer", description = "Mês de nascimento (1 a 12)." },
                anoNascimento = new { type = "integer", description = "Ano de nascimento com 4 dígitos." },
                confirmado = new { type = "boolean", description = "Só true na SEGUNDA chamada, depois que a pessoa confirmar o nome." },
                motivo = new { type = "string", description = "Motivo do não comparecimento, se a pessoa disser." },
            },
            required = new[] { "cpf", "mesNascimento", "anoNascimento" },
        }
        : new
        {
            type = "object",
            properties = new
            {
                cpf = new { type = "string", description = "Os 4 PRIMEIROS dígitos do CPF. Nunca peça o CPF completo." },
                mesNascimento = new { type = "integer", description = "Mês de nascimento (1 a 12)." },
                anoNascimento = new { type = "integer", description = "Ano de nascimento com 4 dígitos." },
                confirmado = new { type = "boolean", description = "Só true na SEGUNDA chamada, depois que a pessoa confirmar o nome." },
            },
            required = new[] { "cpf", "mesNascimento", "anoNascimento" },
        };
}
