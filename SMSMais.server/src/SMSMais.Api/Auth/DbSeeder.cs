using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SMSMais.Core.RoboAtendimento;
using SMSMais.Data;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.Robo;

namespace SMSMais.Api.Auth;

/// <summary>
/// Seeds idempotentes executados no startup, depois das migrations.
/// Garante o Admin inicial (perfil + usuário com senha hash).
/// </summary>
public static class DbSeeder
{
    public static Guid AdminUsuarioId => IdentificadoresFixos.UsuarioAdminId;
    // Login do admin: "admin" (via Email) ou "00000000000" (via Cpf) — o login casa string
    // exata, sem validar formato. Identificadores memoráveis a pedido do usuário.
    private const string AdminEmail = "admin";
    private const string AdminCpf = "00000000000";
    private const string AdminSenhaInicial = "Abc,123!";

    public static async Task SeedAsync(
        SmsMaisDbContext db,
        IPasswordHasher<Usuario> hasher,
        bool incluirConteudoMarica = false,
        CancellationToken cancellationToken = default)
    {
        // Bootstrap GENÉRICO — toda instância precisa: sem ele ninguém consegue logar.
        await GarantirPerfilAdminAsync(db, cancellationToken);
        await GarantirUsuarioAdminAsync(db, hasher, cancellationToken);

        // Assuntos padrão do robô (Número errado, OTP, "cadê o laudo"). Genéricos (úteis a
        // qualquer município) e insert-only — não sobrescrevem edições do operador. A URL do app
        // vem da Instituicao (nunca cravada — whitelabel ADR-0043).
        await GarantirAssuntosPadraoRoboAsync(db, cancellationToken);

        // Conteúdo CLÍNICO DE MARICÁ (macro do CDT, banner de laudo). Atrás de flag
        // (`Seeds:ConteudoMarica`) porque numa instância de outro município isso semearia
        // a marca e os templates da cidade errada no startup — o vazamento mais silencioso
        // do whitelabel (ADR-0043/0046). Maricá já tem os dados no banco; default false
        // não re-semeia nada lá (os Garantir* são insert-only e retornam cedo).
        if (incluirConteudoMarica)
        {
            await GarantirTemplateMamografiaAsync(db, cancellationToken);
            await SeedCabecalhoLaudo.GarantirAsync(db, cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    // Ids fixos dos assuntos padrão — insert-only, permitem reconhecer o já semeado.
    private static readonly Guid AssuntoNumeroErradoId = new("0b0b0b0b-0000-0000-0000-0000000000e1");
    private static readonly Guid AssuntoOtpId = new("0b0b0b0b-0000-0000-0000-0000000000e2");
    private static readonly Guid AssuntoLaudoId = new("0b0b0b0b-0000-0000-0000-0000000000e3");
    private static readonly Guid AssuntoRegulacaoId = new("0b0b0b0b-0000-0000-0000-0000000000e4");
    private static readonly Guid AssuntoConfirmacaoId = new("0b0b0b0b-0000-0000-0000-0000000000e5");
    private static readonly Guid AssuntoCancelamentoId = new("0b0b0b0b-0000-0000-0000-0000000000e6");
    private static readonly Guid AssuntoSaudacaoId = new("0b0b0b0b-0000-0000-0000-0000000000e7");
    private static readonly Guid AssuntoAjudaId = new("0b0b0b0b-0000-0000-0000-0000000000e8");
    private static readonly Guid AssuntoLocalGuiaId = new("0b0b0b0b-0000-0000-0000-0000000000e9");
    private static readonly Guid AssuntoDocumentosId = new("0b0b0b0b-0000-0000-0000-0000000000ea");

    /// <summary>
    /// Semeia os assuntos padrão do robô (insert-only). A URL do app do cidadão vem da
    /// <see cref="Instituicao"/> — NUNCA cravada no código (whitelabel ADR-0043).
    /// </summary>
    private static async Task GarantirAssuntosPadraoRoboAsync(SmsMaisDbContext db, CancellationToken ct)
    {
        var urlApp = await db.Instituicoes.AsNoTracking().Select(i => i.UrlApp).FirstOrDefaultAsync(ct);
        var podeAcompanhar = string.IsNullOrWhiteSpace(urlApp)
            ? "A pessoa pode acompanhar tudo no aplicativo do cidadão da prefeitura."
            : $"A pessoa pode acompanhar tudo no app do cidadão em {urlApp}.";
        var lembreteCadastro =
            "Quando fizer sentido, lembre a pessoa de manter o cadastro (telefone e dados) atualizado no app.";

        await GarantirAssuntoNumeroErradoAsync(db, ct);
        await GarantirAssuntoOtpAsync(db, podeAcompanhar, ct);
        await GarantirAssuntoLaudoAsync(db, podeAcompanhar, lembreteCadastro, ct);
        await GarantirAssuntoRegulacaoAsync(db, podeAcompanhar, ct);
        await GarantirAssuntoConfirmacaoAsync(db, ct);
        await GarantirAssuntoCancelamentoAsync(db, ct);
        await GarantirAssuntoSaudacaoAsync(db, ct);
        await GarantirAssuntoAjudaAsync(db, ct);
        await GarantirAssuntoLocalGuiaAsync(db, ct);
        await GarantirAssuntoDocumentosAsync(db, ct);
        await GarantirAssuntoVerificacaoCadastralAsync(db, ct);
    }

    /// <summary>
    /// "Verificação cadastral": estado — a pessoa respondeu ao desafio dos 4 primeiros dígitos do
    /// CPF (template validacao_cadastro). O robô valida (VerificarCadastro) e libera a confirmação
    /// real. Sem condições: é roteado por ESTADO (não por palavra-chave).
    /// </summary>
    private static async Task GarantirAssuntoVerificacaoCadastralAsync(SmsMaisDbContext db, CancellationToken ct)
    {
        if (await db.RoboAssuntos.AnyAsync(a => a.Id == RoboAssuntosPadrao.VerificacaoCadastralId, ct)) return;

        db.RoboAssuntos.Add(new RoboAssunto
        {
            Id = RoboAssuntosPadrao.VerificacaoCadastralId,
            Nome = "Verificação cadastral",
            Descricao = "A pessoa está confirmando os 4 primeiros dígitos do CPF para receber a confirmação do agendamento.",
            InstrucoesPersona = "Atenda quando a pessoa está confirmando a identidade após o pedido dos 4 primeiros dígitos do CPF.",
            Ativo = true,
            MaxInteracoesSemResolver = 4,
            LimiarConfianca = 0.6,
            Ordem = 0,
            CriadoEm = DateTime.UtcNow,
            Condicoes = [],
            Treinos =
            [
                Treino(TipoTreinoRobo.Instrucao, "Por que os 4 dígitos",
                    "A pessoa recebeu um pedido para confirmar os 4 primeiros dígitos do CPF, para protegermos uma informação de saúde antes de mostrá-la."),
                Treino(TipoTreinoRobo.Do, "Validar",
                    "Quando a pessoa enviar os 4 dígitos (ou o CPF inteiro), use 'verificar_cadastro' com cpf."),
                Treino(TipoTreinoRobo.Do, "Atendente = acalmar, não desviar",
                    "Se pedir atendente ou hesitar, explique que os 4 dígitos protegem uma informação de saúde SENSÍVEL, é rápido e seguro; pergunte se é a própria pessoa ou parente/responsável e tente concluir a verificação antes de encaminhar."),
                Treino(TipoTreinoRobo.Do, "Não sou essa pessoa",
                    "Se disser que não é a pessoa, pergunte se é parente/responsável e use 'registrar_numero_errado'."),
                Treino(TipoTreinoRobo.Dont, "Sem outros dados",
                    "Não exija o CPF inteiro nem outros dados; os 4 primeiros dígitos bastam."),
                Treino(TipoTreinoRobo.Do, "Persistiu → humano",
                    "Se não conseguir validar após tentar, encaminhe ao atendente humano."),
            ],
            Comandos =
            [
                Comando(ComandoRobo.VerificarCadastro),
                Comando(ComandoRobo.RegistrarNumeroErrado),
                Comando(ComandoRobo.EncaminharParaHumano),
            ],
        });
    }

    /// <summary>
    /// "Número errado / Não sou essa pessoa": o robô NÃO corrige cadastro — pergunta se o contato
    /// é parente/responsável, registra a pendência e pode encaminhar ao humano.
    /// </summary>
    private static async Task GarantirAssuntoNumeroErradoAsync(SmsMaisDbContext db, CancellationToken ct)
    {
        if (await db.RoboAssuntos.AnyAsync(a => a.Id == AssuntoNumeroErradoId, ct)) return;

        db.RoboAssuntos.Add(new RoboAssunto
        {
            Id = AssuntoNumeroErradoId,
            Nome = "Número errado / Não sou essa pessoa",
            Descricao = "O cidadão diz que a mensagem não é para ele / o número não é da pessoa do cadastro.",
            InstrucoesPersona =
                "Atenda quando a pessoa diz que a mensagem não é para ela, que não é essa pessoa, ou que o "
                + "número não é de quem a mensagem cita.",
            Ativo = true,
            MaxInteracoesSemResolver = 4,
            LimiarConfianca = 0.6,
            Ordem = 0,
            CriadoEm = DateTime.UtcNow,
            Condicoes =
            [
                Condicao("não sou essa pessoa"), Condicao("nao sou essa pessoa"), Condicao("não sou eu"),
                Condicao("número errado"), Condicao("numero errado"), Condicao("pessoa errada"),
                Condicao("não conheço"), Condicao("nao conheco"), Condicao("engano"),
            ],
            Treinos =
            [
                Treino(TipoTreinoRobo.Dont, "Não corrigir cadastro",
                    "Nunca prometa corrigir o cadastro nem confirme dados do paciente."),
                Treino(TipoTreinoRobo.Do, "Perguntar o vínculo",
                    "Pergunte gentilmente se a pessoa é parente, responsável, ou se não tem vínculo (foi engano)."),
                Treino(TipoTreinoRobo.Instrucao, "Família compartilha o número",
                    "É comum uma família usar o mesmo número (a mãe atende pelo filho, etc.). Trate com naturalidade; o objetivo é saber o vínculo."),
                Treino(TipoTreinoRobo.Do, "Registrar o vínculo",
                    "Com a resposta, use 'registrar_numero_errado' informando o vínculo."),
                Treino(TipoTreinoRobo.Do, "Fechar com cordialidade",
                    "Agradeça e explique que a equipe vai revisar o cadastro."),
                Treino(TipoTreinoRobo.Dont, "Sem dados pessoais",
                    "Não peça documentos nem dados pessoais."),
                Treino(TipoTreinoRobo.Do, "Dúvida ou pedido → humano",
                    "Se a pessoa pedir atendimento humano ou ficar em dúvida, use 'encaminhar_para_humano'."),
                Treino(TipoTreinoRobo.Exemplo, "Exemplo de resposta",
                    "Cidadão: \"não sou essa pessoa\" → Robô: \"Obrigado por avisar! Só para registrarmos: " +
                    "você é parente ou responsável por essa pessoa, ou foi engano?\""),
            ],
            Comandos = [Comando(ComandoRobo.RegistrarNumeroErrado), Comando(ComandoRobo.EncaminharParaHumano)],
        });
    }

    /// <summary>"Código de verificação (OTP)": explica o código de 6 dígitos; nunca pedir o código.</summary>
    private static async Task GarantirAssuntoOtpAsync(SmsMaisDbContext db, string podeAcompanhar, CancellationToken ct)
    {
        if (await db.RoboAssuntos.AnyAsync(a => a.Id == AssuntoOtpId, ct)) return;

        db.RoboAssuntos.Add(new RoboAssunto
        {
            Id = AssuntoOtpId,
            Nome = "Código de verificação (OTP)",
            Descricao = "O cidadão pergunta o que é o código de 6 números que recebeu.",
            InstrucoesPersona = "Atenda quando a pessoa pergunta o que é o código de 6 números/dígitos que recebeu.",
            Ativo = true,
            MaxInteracoesSemResolver = 4,
            LimiarConfianca = 0.6,
            Ordem = 1,
            CriadoEm = DateTime.UtcNow,
            Condicoes =
            [
                Condicao("código"), Condicao("codigo"), Condicao("6 números"), Condicao("6 numeros"),
                Condicao("seis dígitos"), Condicao("que número é esse"), Condicao("pra que serve o código"),
                Condicao("código que chegou"),
            ],
            Treinos =
            [
                Treino(TipoTreinoRobo.Instrucao, "O que é o código",
                    "É um código de verificação, para confirmar a identidade e entrar no app do cidadão com segurança; é normal e temporário."),
                Treino(TipoTreinoRobo.Do, "Nunca compartilhar",
                    "Oriente a nunca compartilhar o código com ninguém — nem com atendentes."),
                Treino(TipoTreinoRobo.Instrucao, "Se não pediu",
                    "Se a pessoa não tentou entrar ou validar nada, pode simplesmente ignorar."),
                Treino(TipoTreinoRobo.Do, "Acompanhar no app", podeAcompanhar),
                Treino(TipoTreinoRobo.Dont, "Nunca pedir o código",
                    "Jamais peça, repita ou confirme o código do cidadão."),
                Treino(TipoTreinoRobo.Dont, "Sem dados pessoais",
                    "Não peça CPF, senha ou dados pessoais para 'explicar' o código."),
                Treino(TipoTreinoRobo.Do, "Insistência → humano",
                    "Se a pessoa insistir em atendimento humano, use 'encaminhar_para_humano'."),
            ],
            Comandos = [Comando(ComandoRobo.EncaminharParaHumano)],
        });
    }

    /// <summary>"Cadê o laudo": a imagem chega antes; o laudo é enviado quando o médico conclui.</summary>
    private static async Task GarantirAssuntoLaudoAsync(
        SmsMaisDbContext db, string podeAcompanhar, string lembreteCadastro, CancellationToken ct)
    {
        if (await db.RoboAssuntos.AnyAsync(a => a.Id == AssuntoLaudoId, ct)) return;

        db.RoboAssuntos.Add(new RoboAssunto
        {
            Id = AssuntoLaudoId,
            Nome = "Cadê o laudo / vai ter laudo",
            Descricao = "O cidadão recebeu a imagem do exame e pergunta pelo laudo/resultado.",
            InstrucoesPersona =
                "Atenda quando a pessoa recebeu a imagem do exame e pergunta 'cadê o laudo' / 'vai ter laudo' "
                + "/ 'quando sai o resultado'.",
            Ativo = true,
            MaxInteracoesSemResolver = 5,
            LimiarConfianca = 0.6,
            Ordem = 2,
            CriadoEm = DateTime.UtcNow,
            Condicoes =
            [
                Condicao("cadê o laudo"), Condicao("cade o laudo"), Condicao("e o laudo"),
                Condicao("vai ter laudo"), Condicao("quando sai o laudo"), Condicao("quando fica pronto o laudo"),
                Condicao("resultado do exame"), Condicao("meu resultado"),
            ],
            Treinos =
            [
                Treino(TipoTreinoRobo.Instrucao, "Ordem imagem → laudo",
                    "A imagem chega primeiro; o laudo é escrito pelo médico e enviado quando pronto — a pessoa será avisada. Isso é normal."),
                Treino(TipoTreinoRobo.Do, "Confirmar identidade antes do dado",
                    "Antes de qualquer dado, confirme o NOME e os 4 PRIMEIROS DÍGITOS DO CPF (aceite CPF inteiro/mais dígitos; use os 4 primeiros)."),
                Treino(TipoTreinoRobo.Do, "Consultar só após conferir",
                    "Só então use 'consultar_status_exame_recente' com o nome e o CPF informados."),
                Treino(TipoTreinoRobo.Dont, "Não vazar sem conferir",
                    "Se o comando indicar que não confere, não revele nada e ofereça atendimento humano."),
                Treino(TipoTreinoRobo.Dont, "Sem prazo nem dado clínico",
                    "Não prometa data nem confirme dados clínicos além da situação (pronto / em elaboração)."),
                Treino(TipoTreinoRobo.Do, "Acompanhar no app", podeAcompanhar),
                Treino(TipoTreinoRobo.Do, "Cadastro atualizado", lembreteCadastro),
                Treino(TipoTreinoRobo.Do, "Pedido → humano",
                    "Se a pessoa pedir atendimento humano, use 'encaminhar_para_humano'."),
            ],
            Comandos =
            [
                Comando(ComandoRobo.ConsultarStatusExameRecente),
                Comando(ComandoRobo.EncaminharParaHumano),
            ],
        });
    }

    /// <summary>
    /// "Posição do agendamento (regulação)": SER/SISREG/SERNIT. Dado MINIMIZADO — em regra só
    /// "em fila"; pode citar tentativas de contato (followup); nada quando há pendência; e pede
    /// atendente quando cancelado. Nunca expõe dados internos do agendamento.
    /// </summary>
    private static async Task GarantirAssuntoRegulacaoAsync(SmsMaisDbContext db, string podeAcompanhar, CancellationToken ct)
    {
        if (await db.RoboAssuntos.AnyAsync(a => a.Id == AssuntoRegulacaoId, ct)) return;

        db.RoboAssuntos.Add(new RoboAssunto
        {
            Id = AssuntoRegulacaoId,
            Nome = "Posição do agendamento (regulação)",
            Descricao = "O cidadão pergunta a situação de um agendamento/consulta/exame na regulação (SER/SISREG/SERNIT).",
            InstrucoesPersona =
                "Atenda quando a pessoa pergunta a posição/situação de um agendamento, consulta ou exame na "
                + "regulação (SER, SISREG ou SERNIT).",
            Ativo = true,
            MaxInteracoesSemResolver = 5,
            LimiarConfianca = 0.6,
            Ordem = 3,
            CriadoEm = DateTime.UtcNow,
            Condicoes =
            [
                Condicao("meu agendamento"), Condicao("minha consulta marcada"), Condicao("posição da fila"),
                Condicao("regulação"), Condicao("sisreg"), Condicao("meu exame marcado"),
                Condicao("cadê minha consulta"), Condicao("quando vou ser chamado"), Condicao("status do agendamento"),
            ],
            Treinos =
            [
                Treino(TipoTreinoRobo.Do, "Consultar a situação",
                    "Use 'consultar_posicao_regulacao' para verificar a situação."),
                Treino(TipoTreinoRobo.Do, "Confirmar identidade se pedir",
                    "Se o comando pedir confirmação de identidade, peça os 4 primeiros dígitos do CPF e o mês e ano de nascimento e chame de novo (número verificado dispensa)."),
                Treino(TipoTreinoRobo.Instrucao, "Padrão: só 'em fila'",
                    "Em regra, informe apenas que a solicitação está em fila aguardando regulação."),
                Treino(TipoTreinoRobo.Do, "Tentativas de contato",
                    "Se o comando indicar tentativas de contato sem sucesso, pode dizer que houve tentativas (quantas/quando) e pedir para manter o telefone atualizado."),
                Treino(TipoTreinoRobo.Instrucao, "Com pendência",
                    "Se houver pendência, não detalhe — informe apenas que está em fila."),
                Treino(TipoTreinoRobo.Do, "Cancelado → atendente",
                    "Se estiver cancelado, diga que está em fila e que um atendente vai entrar em contato; use 'encaminhar_para_humano'."),
                Treino(TipoTreinoRobo.Dont, "Nunca dados internos",
                    "Nunca revele datas internas, códigos, unidade, procedimento, profissional ou prioridade."),
                Treino(TipoTreinoRobo.Do, "Acompanhar no app", podeAcompanhar),
            ],
            Comandos = [Comando(ComandoRobo.ConsultarPosicaoRegulacao), Comando(ComandoRobo.EncaminharParaHumano)],
        });
    }

    /// <summary>"Confirmação de presença" em TEXTO LIVRE (sim/confirmo/confirmado…) — derivado das
    /// conversas reais (milhares confirmam por texto, fora do botão). Registra com guard.</summary>
    private static async Task GarantirAssuntoConfirmacaoAsync(SmsMaisDbContext db, CancellationToken ct)
    {
        if (await db.RoboAssuntos.AnyAsync(a => a.Id == AssuntoConfirmacaoId, ct)) return;

        db.RoboAssuntos.Add(new RoboAssunto
        {
            Id = AssuntoConfirmacaoId,
            Nome = "Confirmação de presença (texto livre)",
            Descricao = "A pessoa responde confirmando presença sem usar o botão (ex.: 'sim', 'confirmo', 'confirmado').",
            InstrucoesPersona = "Atenda quando a pessoa responde confirmando presença em um agendamento por texto.",
            Ativo = true,
            MaxInteracoesSemResolver = 3,
            LimiarConfianca = 0.6,
            Ordem = 4,
            CriadoEm = DateTime.UtcNow,
            Condicoes =
            [
                Condicao("confirmo"), Condicao("confirmado"), Condicao("pode confirmar"), Condicao("confirmar"),
                Condicao("sim confirmo"), Condicao("estarei la"), Condicao("vou sim"), Condicao("irei"),
                Condicao("confirmo minha presenca"), Condicao("confirmo o comparecimento"),
                Condicao("presenca confirmada"), Condicao("ciente"), Condicao("estou ciente"),
            ],
            Treinos =
            [
                Treino(TipoTreinoRobo.Do, "Confirmar identidade antes",
                    "Antes de confirmar, peça os 4 PRIMEIROS DÍGITOS DO CPF e o MÊS E ANO de nascimento (aceite CPF inteiro)."),
                Treino(TipoTreinoRobo.Do, "Mostrar o agendamento e confirmar",
                    "Use 'confirmar_presenca' com cpf, mesNascimento e anoNascimento; ao receber o retorno, MOSTRE à pessoa qual agendamento foi confirmado (procedimento e data) e agradeça."),
                Treino(TipoTreinoRobo.Dont, "Não confirmar sem conferir",
                    "Se a identidade não conferir ou não houver agendamento, NÃO confirme; peça de novo ou encaminhe ao humano."),
            ],
            Comandos = [Comando(ComandoRobo.ConfirmarPresenca), Comando(ComandoRobo.EncaminharParaHumano)],
        });
    }

    /// <summary>"Cancelamento / não poderei ir" em texto livre — registra a intenção (a equipe decide).</summary>
    private static async Task GarantirAssuntoCancelamentoAsync(SmsMaisDbContext db, CancellationToken ct)
    {
        if (await db.RoboAssuntos.AnyAsync(a => a.Id == AssuntoCancelamentoId, ct)) return;

        db.RoboAssuntos.Add(new RoboAssunto
        {
            Id = AssuntoCancelamentoId,
            Nome = "Não poderei ir / cancelamento",
            Descricao = "A pessoa avisa que não poderá comparecer ao agendamento.",
            InstrucoesPersona = "Atenda quando a pessoa avisa que NÃO poderá comparecer ao agendamento.",
            Ativo = true,
            MaxInteracoesSemResolver = 3,
            LimiarConfianca = 0.6,
            Ordem = 5,
            CriadoEm = DateTime.UtcNow,
            Condicoes =
            [
                Condicao("nao poderei ir"), Condicao("nao poderei comparecer"), Condicao("nao vou poder"),
                Condicao("nao consigo ir"), Condicao("preciso remarcar"), Condicao("quero cancelar"),
                Condicao("cancelar"), Condicao("desmarcar"),
            ],
            Treinos =
            [
                Treino(TipoTreinoRobo.Do, "Confirmar identidade antes de cancelar",
                    "Cancelar é sensível: antes, confirme a identidade — peça os 4 PRIMEIROS DÍGITOS DO CPF e o MÊS E ANO de nascimento (aceite CPF inteiro)."),
                Treino(TipoTreinoRobo.Do, "Registrar só após conferir",
                    "Só então use 'iniciar_cancelamento' com cpf, mesNascimento e anoNascimento. Se não conferir, NÃO cancele; peça de novo ou encaminhe ao humano."),
                Treino(TipoTreinoRobo.Do, "Orientação padrão de remarcação",
                    "Oriente: caso não seja possível comparecer, retorne ao posto de saúde para uma nova marcação em data futura."),
                Treino(TipoTreinoRobo.Dont, "Não remarca pelo WhatsApp",
                    "Não remarque nem cancele por conta própria; a nova marcação é feita no posto."),
                Treino(TipoTreinoRobo.Do, "Dúvida → humano",
                    "Se a pessoa insistir ou tiver dúvida sobre a remarcação, encaminhe ao atendente humano."),
            ],
            Comandos = [Comando(ComandoRobo.IniciarCancelamento), Comando(ComandoRobo.EncaminharParaHumano)],
        });
    }

    /// <summary>Saudações e agradecimentos (bom dia, obrigada, de nada…) — responder breve e cordial.</summary>
    private static async Task GarantirAssuntoSaudacaoAsync(SmsMaisDbContext db, CancellationToken ct)
    {
        if (await db.RoboAssuntos.AnyAsync(a => a.Id == AssuntoSaudacaoId, ct)) return;

        db.RoboAssuntos.Add(new RoboAssunto
        {
            Id = AssuntoSaudacaoId,
            Nome = "Saudações e agradecimentos",
            Descricao = "Cumprimentos e agradecimentos do cidadão (bom dia, obrigada, de nada).",
            InstrucoesPersona = "Atenda saudações e agradecimentos.",
            Ativo = true,
            MaxInteracoesSemResolver = 2,
            LimiarConfianca = 0.55,
            Ordem = 9,
            CriadoEm = DateTime.UtcNow,
            Condicoes =
            [
                Condicao("bom dia"), Condicao("boa tarde"), Condicao("boa noite"), Condicao("ola"),
                Condicao("oi"), Condicao("obrigado"), Condicao("obrigada"), Condicao("de nada"),
                Condicao("gratidao"), Condicao("tudo bem"),
            ],
            Treinos =
            [
                Treino(TipoTreinoRobo.Do, "Cordial e breve", "Responda com cordialidade e brevidade."),
                Treino(TipoTreinoRobo.Do, "Oferecer ajuda", "Pergunte, de forma gentil, em que pode ajudar."),
                Treino(TipoTreinoRobo.Dont, "Não se alongar", "Não faça perguntas desnecessárias nem envie textos longos."),
            ],
            Comandos = [Comando(ComandoRobo.EncaminharParaHumano)],
        });
    }

    /// <summary>"Falar com atendente" / ajuda genérica — o maior volume real. Triar antes de encaminhar.</summary>
    private static async Task GarantirAssuntoAjudaAsync(SmsMaisDbContext db, CancellationToken ct)
    {
        if (await db.RoboAssuntos.AnyAsync(a => a.Id == AssuntoAjudaId, ct)) return;

        db.RoboAssuntos.Add(new RoboAssunto
        {
            Id = AssuntoAjudaId,
            Nome = "Falar com atendente / ajuda geral",
            Descricao = "A pessoa pede atendente ou ajuda de forma genérica.",
            InstrucoesPersona = "Atenda quando a pessoa pede para falar com um atendente ou pede ajuda de forma genérica.",
            Ativo = true,
            MaxInteracoesSemResolver = 3,
            LimiarConfianca = 0.5,
            Ordem = 10,
            CriadoEm = DateTime.UtcNow,
            Condicoes =
            [
                Condicao("falar com um atendente"), Condicao("falar com atendente"), Condicao("atendente"),
                Condicao("preciso de ajuda"), Condicao("ajuda"), Condicao("preciso de ajuda com a saude"),
            ],
            Treinos =
            [
                Treino(TipoTreinoRobo.Do, "Tentar entender antes",
                    "Pergunte, de forma cordial, do que a pessoa precisa, para tentar ajudar antes de acionar um atendente."),
                Treino(TipoTreinoRobo.Do, "Encaminhar quando preciso",
                    "Se não puder resolver ou a pessoa insistir por atendimento humano, use 'encaminhar_para_humano'."),
                Treino(TipoTreinoRobo.Instrucao, "Fora do horário",
                    "Fora do horário de atendimento, informe o horário e oriente o retorno em vez de deixar sem resposta."),
            ],
            Comandos = [Comando(ComandoRobo.EncaminharParaHumano), Comando(ComandoRobo.InformarHorarioAtendimento)],
        });
    }

    /// <summary>"Onde é o exame / retirar a guia" — resposta canônica das atendentes: retire a guia
    /// no posto onde é cadastrado; o local é informado lá. O robô NUNCA inventa endereço.</summary>
    private static async Task GarantirAssuntoLocalGuiaAsync(SmsMaisDbContext db, CancellationToken ct)
    {
        if (await db.RoboAssuntos.AnyAsync(a => a.Id == AssuntoLocalGuiaId, ct)) return;

        db.RoboAssuntos.Add(new RoboAssunto
        {
            Id = AssuntoLocalGuiaId,
            Nome = "Local do exame / retirar a guia",
            Descricao = "A pessoa pergunta onde será o exame/consulta ou onde retira a guia de agendamento.",
            InstrucoesPersona = "Atenda quando a pessoa pergunta ONDE será o exame/consulta, o endereço, a clínica, ou onde retira a guia.",
            Ativo = true,
            MaxInteracoesSemResolver = 3,
            LimiarConfianca = 0.55,
            Ordem = 6,
            CriadoEm = DateTime.UtcNow,
            Condicoes =
            [
                Condicao("onde sera"), Condicao("vai ser aonde"), Condicao("onde fica"), Condicao("qual clinica"),
                Condicao("qual o local"), Condicao("endereco"), Condicao("onde faco"), Condicao("onde e o exame"),
                Condicao("onde retiro a guia"), Condicao("pra onde"), Condicao("qual endereco"), Condicao("aonde"),
            ],
            Treinos =
            [
                Treino(TipoTreinoRobo.Instrucao, "A guia sai no posto",
                    "A guia de agendamento é retirada no POSTO DE SAÚDE onde a pessoa é cadastrada; o local do atendimento é informado lá."),
                Treino(TipoTreinoRobo.Do, "Orientar o posto",
                    "Oriente a procurar o posto de saúde onde é cadastrada para retirar a guia e confirmar o local do atendimento."),
                Treino(TipoTreinoRobo.Dont, "Nunca inventar local",
                    "Nunca invente endereço, nome de clínica ou unidade de atendimento."),
                Treino(TipoTreinoRobo.Do, "Dúvida → humano",
                    "Se a pessoa insistir por um endereço específico, encaminhe ao atendente humano."),
            ],
            Comandos = [Comando(ComandoRobo.EncaminharParaHumano)],
        });
    }

    /// <summary>"Preciso levar o pedido / documentos" — leve o pedido médico e retire a guia no posto.</summary>
    private static async Task GarantirAssuntoDocumentosAsync(SmsMaisDbContext db, CancellationToken ct)
    {
        if (await db.RoboAssuntos.AnyAsync(a => a.Id == AssuntoDocumentosId, ct)) return;

        db.RoboAssuntos.Add(new RoboAssunto
        {
            Id = AssuntoDocumentosId,
            Nome = "Preciso levar o pedido / documentos",
            Descricao = "A pessoa pergunta o que precisa levar (pedido médico, guia, documentos).",
            InstrucoesPersona = "Atenda quando a pessoa pergunta o que precisa levar ou se precisa do pedido médico.",
            Ativo = true,
            MaxInteracoesSemResolver = 3,
            LimiarConfianca = 0.55,
            Ordem = 7,
            CriadoEm = DateTime.UtcNow,
            Condicoes =
            [
                Condicao("preciso levar o pedido"), Condicao("tem que levar o pedido"), Condicao("o que levar"),
                Condicao("levar documentos"), Condicao("voces tem o pedido"), Condicao("preciso do pedido"),
                Condicao("precisa do pedido"), Condicao("o que preciso levar"),
            ],
            Treinos =
            [
                Treino(TipoTreinoRobo.Instrucao, "Pedido + guia no posto",
                    "Para o exame/consulta, leve o pedido médico e retire a guia de agendamento no posto de saúde onde é cadastrado."),
                Treino(TipoTreinoRobo.Do, "Orientar o posto",
                    "Oriente a retirar a guia no posto; lá também confirmam o que levar e o local."),
                Treino(TipoTreinoRobo.Dont, "Não inventar exigências",
                    "Não invente exigências de documentos que você não tem certeza."),
                Treino(TipoTreinoRobo.Do, "Dúvida → humano",
                    "Em dúvida, encaminhe ao atendente humano."),
            ],
            Comandos = [Comando(ComandoRobo.EncaminharParaHumano)],
        });
    }

    private static RoboAssuntoCondicao Condicao(string valor) => new()
    {
        Id = Guid.CreateVersion7(),
        Tipo = TipoCondicaoRobo.PalavraChave,
        Valor = valor,
        Ativo = true,
    };

    private static RoboAssuntoTreino Treino(TipoTreinoRobo tipo, string titulo, string conteudo) => new()
    {
        Id = Guid.CreateVersion7(),
        Tipo = tipo,
        Titulo = titulo,
        Conteudo = conteudo,
        Ativo = true,
    };

    private static RoboAssuntoComando Comando(ComandoRobo cmd) => new()
    {
        Id = Guid.CreateVersion7(),
        Comando = cmd,
        Habilitado = true,
    };

    /// <summary>
    /// Template "Mamografia Digital Bilateral (CDT)" — macro do CDT Maricá com as
    /// frases padronizadas por seção. Insert-only (não sobrescreve edições do
    /// usuário). Identificação do paciente e CRM/RQE do assinante NÃO entram no
    /// corpo: vêm do cabeçalho do laudo e da assinatura.
    /// </summary>
    private static async Task GarantirTemplateMamografiaAsync(SmsMaisDbContext db, CancellationToken ct)
    {
        var existente = await db.LaudoTemplates
            .FirstOrDefaultAsync(t => t.Id == IdentificadoresFixos.TemplateMamografiaCdtId, ct);

        if (existente is not null)
        {
            // Backfill da estrutura de checklist em bases já semeadas (sem sobrescrever edições).
            if (string.IsNullOrWhiteSpace(existente.EstruturaJson))
            {
                existente.EstruturaJson = TemplateMamografiaEstruturaJson;
                existente.AtualizadoEm = DateTime.UtcNow;
            }
            return;
        }

        db.LaudoTemplates.Add(new LaudoTemplate
        {
            Id = IdentificadoresFixos.TemplateMamografiaCdtId,
            Nome = "Mamografia Digital Bilateral (CDT)",
            Categoria = "Mamografia",
            Descricao = "Macro do CDT Maricá: marque as frases aplicáveis — o texto e o BI-RADS são gerados automaticamente.",
            ConteudoHtml = TemplateMamografiaHtml,
            ConteudoJson = "{}",
            EstruturaJson = TemplateMamografiaEstruturaJson,
            CriadoPorUsuarioId = AdminUsuarioId,
            CriadoEm = DateTime.UtcNow,
            Ativo = true,
        });
    }

    private const string TemplateMamografiaHtml = """
        <h2>MAMOGRAFIA DIGITAL BILATERAL</h2>
        <p><em>Selecione as frases aplicáveis, remova as demais e preencha os campos ____.</em></p>
        <h3>INDICAÇÃO</h3>
        <p>Exame de rastreamento.</p>
        <h3>TÉCNICA</h3>
        <p>Incidências mediolaterais oblíquas e craniocaudais bilaterais.</p>
        <p>Incidências mediolaterais oblíquas e craniocaudais bilaterais, com e sem manobra de Eklund.</p>
        <p>Incidências de ampliação (magnificação) da mama direita/esquerda.</p>
        <h3>DESCRIÇÃO BILATERAL</h3>
        <p>Pele e papilas sem alterações.</p>
        <p>Mamas predominantemente adiposas.</p>
        <p>Mamas com densidades fibroglandulares esparsas.</p>
        <p>Mamas heterogeneamente densas, o que pode ocultar nódulos.</p>
        <p>Mamas extremamente densas, o que diminui a sensibilidade da mamografia.</p>
        <p>Distorção arquitetural bilateral por cirurgia prévia (mastoplastia).</p>
        <p>Implante mamário ânteromuscular/retromuscular bilateralmente, sem sinais de ruptura extracapsular ao método.</p>
        <p>Não há evidência de nódulos definidos.</p>
        <p>Nódulo oval, isodenso, obscurecido/circunscrito, medindo ____ cm, localizado no 1/3 anterior/médio/posterior, do/da mama direita/esquerda.</p>
        <p>Nódulo contendo calcificações em pipoca, compatível com fibroadenoma hialinizado na mama direita/esquerda.</p>
        <p>Assimetria focal localizada no 1/3 anterior/médio/posterior do/da mama direita/esquerda.</p>
        <p>Calcificações de aspecto benigno bilateralmente.</p>
        <p>Calcificação de aspecto benigno na mama direita/esquerda.</p>
        <p>Linfonodos axilares sem alterações ao método.</p>
        <p>Prolongamentos axilares sem alterações expressivas.</p>
        <p>Linfonodos no prolongamento axilar direito/esquerdo, sem alterações ao método.</p>
        <p>Linfonodos axilares não visibilizados.</p>
        <p>Linfonodos não visibilizados na axila direita/esquerda.</p>
        <p>Marcador metálico em alteração cutânea na mama direita/esquerda.</p>
        <h3>ANÁLISE COMPARATIVA</h3>
        <p>Não dispomos de exames anteriores para comparação.</p>
        <p>Não houve alterações significativas em relação à mamografia prévia de ____.</p>
        <p>Documentação radiográfica da mamografia de ____, indisponível para análise comparativa.</p>
        <h3>IMPRESSÃO DIAGNÓSTICA</h3>
        <p>Ausência de sinais radiológicos de malignidade.</p>
        <p>Assimetria focal na mama direita/esquerda.</p>
        <p>Nódulo na mama direita/esquerda.</p>
        <p>Nódulos nas mamas.</p>
        <p>Calcificações agrupadas na mama direita/esquerda.</p>
        <h3>AVALIAÇÃO</h3>
        <p>Categoria ____ (BI-RADS)</p>
        <h3>RECOMENDAÇÃO</h3>
        <p>Recomenda-se correlação com ultrassonografia.</p>
        <p>Rastreamento de rotina conforme a faixa etária/risco.</p>
        <p>Recomenda-se avaliação com mamografia em 6 meses.</p>
        <p>Recomenda-se avaliação com mamografia em 1 ano.</p>
        <p>Recomenda-se correlação com estudo histopatológico.</p>
        <p>Recomenda-se ressecção cirúrgica quando clinicamente apropriado.</p>
        <p><strong>OBS:</strong> Mamas densas, a critério clínico complementar o estudo com ultrassonografia para pesquisa de nódulo oculto.</p>
        """;

    // Versão estruturada do macro: cada frase carrega sua contribuição BI-RADS
    // (regra do achado mais suspeito). Campos {chave} substituem os ____ e as
    // barras "direita/esquerda". Mapeamento clínico conservador — a profissional
    // confirma/ajusta a categoria final no laudo.
    private const string TemplateMamografiaEstruturaJson = """
        {
          "versaoSchema": 1,
          "calculadora": "BI-RADS",
          "secoes": [
            {
              "id": "indicacao",
              "titulo": "INDICAÇÃO",
              "selecao": "unica",
              "itens": [
                { "id": "ind-rastreio", "texto": "Exame de rastreamento.", "birads": null }
              ]
            },
            {
              "id": "tecnica",
              "titulo": "TÉCNICA",
              "selecao": "multipla",
              "itens": [
                { "id": "tec-mlo-cc", "texto": "Incidências mediolaterais oblíquas e craniocaudais bilaterais.", "birads": null },
                { "id": "tec-eklund", "texto": "Incidências mediolaterais oblíquas e craniocaudais bilaterais, com e sem manobra de Eklund.", "birads": null },
                { "id": "tec-ampliacao", "texto": "Incidências de ampliação (magnificação) da {lado}.", "birads": null,
                  "campos": [ { "chave": "lado", "tipo": "opcao", "opcoes": ["mama direita", "mama esquerda"] } ] }
              ]
            },
            {
              "id": "descricao",
              "titulo": "DESCRIÇÃO BILATERAL",
              "selecao": "multipla",
              "itens": [
                { "id": "desc-pele", "texto": "Pele e papilas sem alterações.", "birads": "1" },
                { "id": "desc-adiposas", "texto": "Mamas predominantemente adiposas.", "birads": null },
                { "id": "desc-esparsas", "texto": "Mamas com densidades fibroglandulares esparsas.", "birads": null },
                { "id": "desc-heterogeneas", "texto": "Mamas heterogeneamente densas, o que pode ocultar nódulos.", "birads": null },
                { "id": "desc-extremamente", "texto": "Mamas extremamente densas, o que diminui a sensibilidade da mamografia.", "birads": null },
                { "id": "desc-distorcao-previa", "texto": "Distorção arquitetural bilateral por cirurgia prévia (mastoplastia).", "birads": "2" },
                { "id": "desc-implante", "texto": "Implante mamário ânteromuscular/retromuscular bilateralmente, sem sinais de ruptura extracapsular ao método.", "birads": "2" },
                { "id": "desc-sem-nodulos", "texto": "Não há evidência de nódulos definidos.", "birads": "1" },
                { "id": "desc-nodulo-oval", "texto": "Nódulo oval, isodenso, {tipo}, medindo {medida} cm, localizado no {terco}, da {lado}.", "birads": "3",
                  "campos": [
                    { "chave": "tipo", "tipo": "opcao", "opcoes": ["obscurecido", "circunscrito"] },
                    { "chave": "medida", "tipo": "numero", "sufixo": "cm" },
                    { "chave": "terco", "tipo": "opcao", "opcoes": ["1/3 anterior", "1/3 médio", "1/3 posterior"] },
                    { "chave": "lado", "tipo": "opcao", "opcoes": ["mama direita", "mama esquerda"] }
                  ] },
                { "id": "desc-pipoca", "texto": "Nódulo contendo calcificações em pipoca, compatível com fibroadenoma hialinizado na {lado}.", "birads": "2",
                  "campos": [ { "chave": "lado", "tipo": "opcao", "opcoes": ["mama direita", "mama esquerda"] } ] },
                { "id": "desc-assimetria-focal", "texto": "Assimetria focal localizada no {terco} da {lado}.", "birads": "3",
                  "campos": [
                    { "chave": "terco", "tipo": "opcao", "opcoes": ["1/3 anterior", "1/3 médio", "1/3 posterior"] },
                    { "chave": "lado", "tipo": "opcao", "opcoes": ["mama direita", "mama esquerda"] }
                  ] },
                { "id": "desc-calc-benignas-bilat", "texto": "Calcificações de aspecto benigno bilateralmente.", "birads": "2" },
                { "id": "desc-calc-benigna-lado", "texto": "Calcificação de aspecto benigno na {lado}.", "birads": "2",
                  "campos": [ { "chave": "lado", "tipo": "opcao", "opcoes": ["mama direita", "mama esquerda"] } ] },
                { "id": "desc-linfo-ok", "texto": "Linfonodos axilares sem alterações ao método.", "birads": null },
                { "id": "desc-prolong-ok", "texto": "Prolongamentos axilares sem alterações expressivas.", "birads": null },
                { "id": "desc-linfo-prolong", "texto": "Linfonodos no prolongamento axilar {lado}, sem alterações ao método.", "birads": null,
                  "campos": [ { "chave": "lado", "tipo": "opcao", "opcoes": ["direito", "esquerdo"] } ] },
                { "id": "desc-linfo-nao-vis", "texto": "Linfonodos axilares não visibilizados.", "birads": null },
                { "id": "desc-linfo-nao-vis-lado", "texto": "Linfonodos não visibilizados na axila {lado}.", "birads": null,
                  "campos": [ { "chave": "lado", "tipo": "opcao", "opcoes": ["direita", "esquerda"] } ] },
                { "id": "desc-marcador", "texto": "Marcador metálico em alteração cutânea na {lado}.", "birads": null,
                  "campos": [ { "chave": "lado", "tipo": "opcao", "opcoes": ["mama direita", "mama esquerda"] } ] }
              ]
            },
            {
              "id": "comparativa",
              "titulo": "ANÁLISE COMPARATIVA",
              "selecao": "unica",
              "itens": [
                { "id": "comp-sem-anteriores", "texto": "Não dispomos de exames anteriores para comparação.", "birads": null },
                { "id": "comp-sem-mudanca", "texto": "Não houve alterações significativas em relação à mamografia prévia de {data}.", "birads": null,
                  "campos": [ { "chave": "data", "tipo": "texto" } ] },
                { "id": "comp-indisponivel", "texto": "Documentação radiográfica da mamografia de {data}, indisponível para análise comparativa.", "birads": null,
                  "campos": [ { "chave": "data", "tipo": "texto" } ] }
              ]
            },
            {
              "id": "impressao",
              "titulo": "IMPRESSÃO DIAGNÓSTICA",
              "selecao": "multipla",
              "itens": [
                { "id": "imp-ausencia", "texto": "Ausência de sinais radiológicos de malignidade.", "birads": "1" },
                { "id": "imp-assimetria", "texto": "Assimetria focal na {lado}.", "birads": "3",
                  "campos": [ { "chave": "lado", "tipo": "opcao", "opcoes": ["mama direita", "mama esquerda"] } ] },
                { "id": "imp-nodulo", "texto": "Nódulo na {lado}.", "birads": "3",
                  "campos": [ { "chave": "lado", "tipo": "opcao", "opcoes": ["mama direita", "mama esquerda"] } ] },
                { "id": "imp-nodulos", "texto": "Nódulos nas mamas.", "birads": "3" },
                { "id": "imp-calc-agrupadas", "texto": "Calcificações agrupadas na {lado}.", "birads": "4A",
                  "campos": [ { "chave": "lado", "tipo": "opcao", "opcoes": ["mama direita", "mama esquerda"] } ] }
              ]
            },
            {
              "id": "avaliacao",
              "titulo": "AVALIAÇÃO",
              "selecao": "unica",
              "tipo": "birads",
              "itens": []
            },
            {
              "id": "recomendacao",
              "titulo": "RECOMENDAÇÃO",
              "selecao": "multipla",
              "itens": [
                { "id": "rec-us", "texto": "Recomenda-se correlação com ultrassonografia.", "birads": null },
                { "id": "rec-rotina", "texto": "Rastreamento de rotina conforme a faixa etária/risco.", "birads": null },
                { "id": "rec-6m", "texto": "Recomenda-se avaliação com mamografia em 6 meses.", "birads": null },
                { "id": "rec-1a", "texto": "Recomenda-se avaliação com mamografia em 1 ano.", "birads": null },
                { "id": "rec-histopato", "texto": "Recomenda-se correlação com estudo histopatológico.", "birads": null },
                { "id": "rec-cirurgia", "texto": "Recomenda-se ressecção cirúrgica quando clinicamente apropriado.", "birads": null }
              ]
            },
            {
              "id": "observacoes",
              "titulo": "OBSERVAÇÕES",
              "selecao": "multipla",
              "itens": [
                { "id": "obs-densas", "texto": "Mamas densas — a critério clínico, complementar o estudo com ultrassonografia para pesquisa de nódulo oculto.", "birads": "0" }
              ]
            }
          ]
        }
        """;

    private static async Task GarantirPerfilAdminAsync(SmsMaisDbContext db, CancellationToken ct)
    {
        var perfil = await db.Perfis
            .Include(p => p.Permissoes)
            .FirstOrDefaultAsync(p => p.Id == IdentificadoresFixos.PerfilAdminId, ct);

        if (perfil is null)
        {
            perfil = new Perfil
            {
                Id = IdentificadoresFixos.PerfilAdminId,
                Nome = "Administrador",
                Descricao = "Acesso completo a todos os módulos do sistema.",
                Ativo = true,
                CriadoEm = DateTime.UtcNow,
            };
            db.Perfis.Add(perfil);
        }

        // Garante todas as ações em todos os módulos (idempotente).
        var existentes = perfil.Permissoes.ToDictionary(p => p.Modulo);
        foreach (var modulo in Enum.GetValues<ModuloPermissao>())
        {
            if (existentes.TryGetValue(modulo, out var atual))
            {
                if (atual.Acoes != AcoesPermissao.Todas) atual.Acoes = AcoesPermissao.Todas;
            }
            else
            {
                perfil.Permissoes.Add(new PermissaoPerfil
                {
                    PerfilId = perfil.Id,
                    Modulo = modulo,
                    Acoes = AcoesPermissao.Todas,
                });
            }
        }
    }

    private static async Task GarantirUsuarioAdminAsync(
        SmsMaisDbContext db,
        IPasswordHasher<Usuario> hasher,
        CancellationToken ct)
    {
        var existe = await db.Usuarios
            .Include(u => u.UsuariosPerfis)
            .FirstOrDefaultAsync(u => u.Id == AdminUsuarioId, ct);

        if (existe is null)
        {
            var admin = new Usuario
            {
                Id = AdminUsuarioId,
                NomeCompleto = "Administrador",
                Email = AdminEmail,
                Cpf = AdminCpf,
                Ativo = true,
                // Só na CRIAÇÃO (ambiente novo): alguém precisa nascer com acesso global,
                // senão ninguém consegue conceder a ninguém. Num ambiente já existente o
                // seeder não passa por aqui — inclusive porque este usuário genérico deve
                // ser desativado assim que houver administradores com nome próprio.
                AcessoGlobal = true,
                CriadoEm = DateTime.UtcNow,
                SenhaHash = string.Empty,
            };
            admin.SenhaHash = hasher.HashPassword(admin, AdminSenhaInicial);
            admin.UsuariosPerfis.Add(new UsuarioPerfil
            {
                UsuarioId = AdminUsuarioId,
                PerfilId = IdentificadoresFixos.PerfilAdminId,
            });
            db.Usuarios.Add(admin);
            return;
        }

        // Garante o vínculo com o perfil Admin se faltar (não toca a senha).
        if (!existe.UsuariosPerfis.Any(up => up.PerfilId == IdentificadoresFixos.PerfilAdminId))
        {
            existe.UsuariosPerfis.Add(new UsuarioPerfil
            {
                UsuarioId = AdminUsuarioId,
                PerfilId = IdentificadoresFixos.PerfilAdminId,
            });
        }
    }
}
