using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Identidade;
using SMSMais.Core.Integracoes.SernitWeb;
using SMSMais.Core.Integracoes.SernitWeb.Varredura;
using SMSMais.Core.Integracoes.SerWeb;
using SMSMais.Core.Integracoes.SerWeb.Varredura;
using SMSMais.Core.Pacientes;
using SMSMais.Core.Regulacao.Anexos;
using SMSMais.Core.Regulacao.Catalogo;
using SMSMais.Core.Regulacao.Formularios;
using SMSMais.Core.Regulacao.Medicos;
using SMSMais.Core.Regulacao.Solicitacoes;
using SMSMais.Core.Ser.Criacao;
using SMSMais.Core.Ser.Dtos;
using SMSMais.Core.Ser.Sessao;
using SMSMais.Core.Sernit.Sessao;
using SMSMais.Data;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.Ser;
using SMSMais.Data.Entities.Sernit;

namespace SMSMais.Core.Regulacao.EnvioSer;

/// <summary>Um passo do preenchimento: o que foi posto em qual campo do sistema, e se ele aceitou.</summary>
public sealed record EnvioSerPassoDto(string Campo, string? Valor, bool Ok, string? Observacao = null);

/// <summary>Um anexo como vai ao sistema.</summary>
public sealed record EnvioSerAnexoDto(string Nome, long Tamanho, int ArquivosJuntados);

/// <summary>Pedido que o sistema já tem para o mesmo paciente e o mesmo recurso.</summary>
public sealed record EnvioSerDuplicadoDto(string IdSer, string? Recurso, string? DataSolicitacao, string? Situacao);

/// <summary>A prévia: a tela do sistema preenchida inteira, sem anexar nem gravar.</summary>
/// <param name="Sistema">"SER" ou "SERNIT" — para a tela dizer onde vai.</param>
/// <param name="MedicoNovo">O médico pedido pela unidade ainda não está na lista do sistema: a
/// pergunta ao regulador (é um destes parecidos? autoriza cadastrar?). Enquanto houver, não envia.</param>
public sealed record EnvioSerPreparoDto(
    string OperadorSer,
    string Recurso,
    IReadOnlyList<EnvioSerPassoDto> Passos,
    IReadOnlyList<EnvioSerAnexoDto> Anexos,
    IReadOnlyList<EnvioSerDuplicadoDto> PossiveisDuplicados,
    string Sistema = "SER",
    MedicoNovoNoSistemaDto? MedicoNovo = null);

/// <summary>
/// O médico pedido na abertura que a lista do sistema (o combo "Médico responsável" de HOJE) não tem.
/// </summary>
/// <param name="Parecidos">Nomes do combo que podem ser o mesmo médico (o sistema abrevia muito:
/// "LAURA BEATRIZ A. RODRIGUES") e o CRM igual no espelho dos profissionais.</param>
/// <param name="Especialidades">As especialidades do modal "Adicionar Médico" do sistema, na ordem dele
/// (sem repetição de rótulo) — a do pedido é texto livre da unidade e quase nunca bate.</param>
/// <param name="EspecialidadeSugerida">A da lista que parece a pedida ("ONCOLOGISTA" → "ONCOLOGIA").</param>
/// <param name="PodeCadastrar">O modal existe na tela e o médico está pendente (não houve tentativa).</param>
public sealed record MedicoNovoNoSistemaDto(
    Guid PendenteId,
    string Nome,
    string? TipoDocumento,
    string? NumeroDocumento,
    string? EspecialidadePedida,
    SituacaoMedicoPendente Situacao,
    IReadOnlyList<MedicoParecidoDto> Parecidos,
    IReadOnlyList<string> Especialidades,
    string? EspecialidadeSugerida,
    bool PodeCadastrar);

/// <summary>"Autorizo cadastrar no sistema" — os dados como vão ao modal "Adicionar Médico".</summary>
/// <param name="Autorizo">Tem de vir <c>true</c>: é a autorização expressa do regulador para escrever no
/// cadastro do Estado, que não tem editar nem apagar.</param>
/// <param name="Especialidade">Rótulo da lista do sistema (<see cref="MedicoNovoNoSistemaDto.Especialidades"/>).</param>
/// <param name="Nome">Vazio = o nome pedido pela unidade.</param>
public sealed record CadastrarMedicoNoSistemaRequest(
    bool Autorizo, string Especialidade, string? Nome, string? TipoDocumento, string? NumeroDocumento);

/// <param name="Desfecho">"Cadastrado" (gravou e o nome apareceu na lista) ou "JaExistia" (o nome
/// já estava lá — nada foi gravado).</param>
public sealed record MedicoCadastradoNoSistemaDto(
    string Desfecho, string NomeNoSistema, string Mensagem, string? MensagemDoSistema);

public sealed record EnviarAoSerRequest(bool EnviarMesmoComPedidoParecido);

/// <summary>O desfecho do envio — vai para o próprio modal, não para um toast.</summary>
public sealed record EnvioSerResultadoDto(
    string NumeroExterno,
    bool Conferido,
    string? MensagemDoSer,
    string OperadorSer,
    IReadOnlyList<EnvioSerPassoDto> Passos,
    RegulacaoSolicitacaoDetalheDto Solicitacao,
    string Sistema = "SER");

/// <summary>
/// Envia uma solicitação da Regulação ao <b>SER-RJ ou ao SERNIT</b> (o SER de Niterói) — <b>a
/// plataforma preenche tudo, anexa e grava</b>, como o envio ao SISCAN na anamnese: primeiro a
/// prévia (percorre a tela sem gravar), depois o envio (grava e RELÊ do sistema para provar).
/// O destino é o da solicitação. Os dois sistemas são a mesma aplicação (JSF + RichFaces + Seam) em
/// instâncias diferentes; o que muda está em <see cref="PerfilTelaCriacao"/> (ADR-0069).
///
/// <para><b>Quem assina é o regulador</b>, com o usuário e a senha DELE no sistema (sessão em
/// memória, amarrada ao login no SMSMais). A credencial de sincronismo nunca escreve.</para>
///
/// <para><b>Nada é achado por número guardado.</b> Recurso, médico e CID são escolhidos pelo NOME
/// na tela de hoje; os campos dinâmicos, pelo rótulo.</para>
///
/// <para><b>Erros voltam com o texto do sistema.</b> Recusa antes do Gravar não cria nada; recusa NO
/// Gravar (mensagem de validação) também não; resposta sem número depois do Gravar é o único caso em
/// que o pedido PODE ter sido criado — e a solicitação fica em "Falha no envio" dizendo para conferir
/// antes de tentar de novo.</para>
/// </summary>
public interface IRegulacaoEnvioSerService
{
    Task<EnvioSerPreparoDto> PrepararAsync(Guid solicitacaoId, CancellationToken ct);

    Task<EnvioSerResultadoDto> EnviarAsync(Guid solicitacaoId, EnviarAoSerRequest req, CancellationToken ct);

    /// <summary>
    /// <b>Cadastra no sistema o médico pedido pela unidade</b> — ESCREVE no cadastro do Estado, pelo
    /// modal "Adicionar Médico" da tela de criação, com a sessão do regulador e o "Autorizo" dele
    /// (ADR-0065, complemento de 08/10/2026). Se o nome já está na lista, não grava: usa o cadastro de
    /// lá. Depois do Gravar, só dá o médico por cadastrado se o nome aparecer na lista; senão o médico
    /// fica "cadastro incerto" e ninguém tenta de novo sem conferir.
    /// </summary>
    Task<MedicoCadastradoNoSistemaDto> CadastrarMedicoAsync(
        Guid solicitacaoId, CadastrarMedicoNoSistemaRequest req, CancellationToken ct);
}

public sealed partial class RegulacaoEnvioSerService(
    SmsMaisDbContext db,
    IRegulacaoSolicitacaoService solicitacoes,
    IRegulacaoFormularioService formularios,
    IArquivoExigenciaStore arquivos,
    ISerSessaoOperadorStore sessoesSer,
    ISernitSessaoOperadorStore sessoesSernit,
    IPacientesService pacientes,
    IRegulacaoMedicoPendenteService medicosPendentes,
    IUsuarioAtualAccessor usuarioAtual,
    ILoggerFactory loggerFactory,
    ILogger<RegulacaoEnvioSerService> logger) : IRegulacaoEnvioSerService
{
    /// <summary>Uma linha da grade de pesquisa do sistema, do jeito que a crítica e a releitura precisam.</summary>
    private sealed record LinhaExterna(
        string Id, string? Recurso, string? Cpf, string? Cns, string? Paciente, string? DataSolicitacao, string? Situacao);

    /// <summary>Filtro da pesquisa: situações em aberto + paciente OU número.</summary>
    private sealed record FiltroExterno(string? Cpf, string? Cns, string? Id);

    /// <summary>Tudo o que muda por sistema, montado uma vez por envio.</summary>
    private sealed record Contexto(
        string Nome,
        ITransporteTelaCriacao Transporte,
        PerfilTelaCriacao Perfil,
        string Operador,
        Func<FiltroExterno, CancellationToken, Task<List<LinhaExterna>>> Pesquisar);

    public async Task<EnvioSerPreparoDto> PrepararAsync(Guid solicitacaoId, CancellationToken ct)
    {
        // Médico ainda pendente não barra a PRÉVIA: ela mostra o que o sistema tem de parecido e
        // pergunta ao regulador. O envio continua barrando.
        var dados = await solicitacoes.PrepararEnvioAutomaticoAsync(solicitacaoId, ct, aceitarMedicoPendente: true);
        var ctx = Contextualizar(dados);
        var anexos = await LerAnexosAsync(ctx, dados, ct);

        var motor = new SerCriacaoSolicitacao(ctx.Transporte, ctx.Perfil, logger);
        var medicoNovo = new MedicoNovoColetado();
        var passos = await PreencherAsync(ctx, motor, dados, ct, medicoNovo);
        var duplicados = await ProcurarParecidosAsync(ctx, dados, ct);

        return new EnvioSerPreparoDto(
            ctx.Operador, dados.RecursoRotulo, passos,
            [.. anexos.Select(a => new EnvioSerAnexoDto(a.Nome, a.Conteudo.LongLength, a.Origens.Count))],
            duplicados, ctx.Nome, medicoNovo.Bloco);
    }

    /// <summary>Onde o preenchimento deixa o bloco do médico novo (só a prévia passa um).</summary>
    private sealed class MedicoNovoColetado
    {
        public MedicoNovoNoSistemaDto? Bloco { get; set; }
    }

    public async Task<MedicoCadastradoNoSistemaDto> CadastrarMedicoAsync(
        Guid solicitacaoId, CadastrarMedicoNoSistemaRequest req, CancellationToken ct)
    {
        var dados = await solicitacoes.PrepararEnvioAutomaticoAsync(solicitacaoId, ct, aceitarMedicoPendente: true);
        var ctx = Contextualizar(dados);
        var sistema = ctx.Nome;

        if (dados.MedicoPendenteId is not { } pendenteId)
        {
            throw new ConflitoException(
                "regulacao.medico_sem_pendencia",
                "O médico desta solicitação já está resolvido. Feche e abra o envio de novo.");
        }
        var pendente = await medicosPendentes.ObterAsync(pendenteId, ct);
        if (pendente.Situacao != SituacaoMedicoPendente.Pendente)
        {
            throw new ConflitoException(
                "regulacao.medico_ja_resolvido",
                $"Já houve uma tentativa de cadastrar este médico no {sistema}. Confira lá e resolva pelo cartão "
                + "do médico na solicitação — tentar de novo pode duplicar o cadastro.");
        }

        // ---- o pedido: autorização expressa e dados como o modal aceita
        if (!req.Autorizo)
        {
            throw new ValidacaoException(
                "autorizo", $"Cadastrar médico no {sistema} precisa da sua autorização expressa.");
        }
        var nome = string.Join(' ', (string.IsNullOrWhiteSpace(req.Nome) ? pendente.Nome : req.Nome)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)).ToUpperInvariant();
        if (nome.Length < 5 || !nome.Contains(' '))
        {
            throw new ValidacaoException("nome", "Digite o nome completo do médico (nome e sobrenome).");
        }
        if (nome.Length > 300) throw new ValidacaoException("nome", "O nome cabe em até 300 caracteres.");
        var tipo = string.IsNullOrWhiteSpace(req.TipoDocumento) ? null : req.TipoDocumento.Trim().ToUpperInvariant();
        var numero = string.IsNullOrWhiteSpace(req.NumeroDocumento) ? null : req.NumeroDocumento.Trim();
        if (tipo is not null && !RegulacaoMedicoPendenteService.TiposDocumento.Contains(tipo))
        {
            throw new ValidacaoException(
                "tipoDocumento",
                $"Tipo de documento deve ser um destes: {string.Join(", ", RegulacaoMedicoPendenteService.TiposDocumento)}.");
        }
        if (numero is not null && tipo is null)
        {
            throw new ValidacaoException("tipoDocumento", "Diga de que é o número (CRM, CNS, RG, CPF…).");
        }
        if (numero is { Length: > 40 }) throw new ValidacaoException("numeroDocumento", "O número cabe em até 40 caracteres.");
        if (string.IsNullOrWhiteSpace(req.Especialidade))
        {
            throw new ValidacaoException("especialidade", $"Escolha a especialidade da lista do {sistema}.");
        }
        var chaveEspecialidade = IdentidadePorNome.Chave(req.Especialidade);

        var motor = new SerCriacaoSolicitacao(ctx.Transporte, ctx.Perfil, logger);
        await motor.AbrirAsync(ct);

        // ---- já está na lista com este nome? Não grava: usa o de lá.
        var iguais = IdentidadePorNome.Achar(motor.Combo(SerCriacaoSolicitacao.CampoMedico), o => o.Rotulo, nome);
        if (iguais.Count > 0)
        {
            var existente = iguais[0].Rotulo.Trim();
            await medicosPendentes.ResolverAsync(
                pendenteId, new ResolverMedicoPendenteRequest(SituacaoMedicoPendente.JaExistia, existente, null), ct);
            return new MedicoCadastradoNoSistemaDto(
                "JaExistia", existente,
                $"O {sistema} já tem \"{existente}\" na lista de médicos — nada foi cadastrado. A solicitação passa a usar esse cadastro.",
                null);
        }

        if (motor.ModalMedico() is not { } modal)
        {
            throw new ValidacaoException(
                "ser.medico", $"A tela do {sistema} não tem o modal \"Adicionar Médico\". Nada foi cadastrado.");
        }
        if (!modal.Especialidades.Any(o => IdentidadePorNome.Chave(o.Rotulo) == chaveEspecialidade))
        {
            throw new ValidacaoException(
                "especialidade", $"\"{req.Especialidade}\" não está na lista de especialidades do {sistema}.");
        }

        // ---- a trava: pendente → "cadastro incerto" ANTES de escrever (duplo clique, dois reguladores,
        // queda do servidor no meio — em todos, o estado que fica é o que manda conferir).
        await medicosPendentes.ReservarCadastroAsync(pendenteId, ct);
        ct = CancellationToken.None;

        try
        {
            var resposta = await motor.CadastrarMedicoAsync(
                nome, tipo, numero,
                opcoes => opcoes.FirstOrDefault(o => IdentidadePorNome.Chave(o.Rotulo) == chaveEspecialidade),
                $"cadastrar o médico {nome} (PR-{dados.NumeroLocal}, autorizado pelo regulador)", ct);
            var mensagem = SerCriacaoSolicitacao.MensagemDaResposta(resposta) is { Length: > 0 } m ? m : motor.Mensagem();

            // A prova: o nome na lista de médicos. Se a resposta não re-renderizou o combo, reabre a tela.
            var achados = IdentidadePorNome.Achar(motor.Combo(SerCriacaoSolicitacao.CampoMedico), o => o.Rotulo, nome);
            if (achados.Count == 0)
            {
                await motor.AbrirAsync(ct);
                achados = IdentidadePorNome.Achar(motor.Combo(SerCriacaoSolicitacao.CampoMedico), o => o.Rotulo, nome);
            }

            if (achados.Count > 0)
            {
                var noSistema = achados[0].Rotulo.Trim();
                await medicosPendentes.ConfirmarCadastroAsync(pendenteId, noSistema, ct);
                logger.LogWarning(
                    "Regulação PR-{Numero}: médico {Nome} CADASTRADO no {Sistema} por {Operador} (conferido na lista).",
                    dados.NumeroLocal, noSistema, sistema, ctx.Operador);
                return new MedicoCadastradoNoSistemaDto(
                    "Cadastrado", noSistema,
                    $"Médico cadastrado no {sistema} e conferido: \"{noSistema}\" já aparece na lista de médicos.",
                    mensagem.Length > 0 ? mensagem : null);
            }

            if (mensagem.Length > 0 && RegexRecusa().IsMatch(mensagem))
            {
                // Mensagem de validação e o nome fora da lista: o sistema recusou, nada foi criado.
                await medicosPendentes.LiberarCadastroAsync(pendenteId, ct);
                logger.LogWarning(
                    "Regulação PR-{Numero}: o {Sistema} recusou o cadastro do médico {Nome}: {Mensagem}",
                    dados.NumeroLocal, sistema, nome, mensagem);
                throw new ValidacaoException(
                    "ser.medico", $"O {sistema} recusou o cadastro: \"{mensagem}\". Nada foi cadastrado — corrija e tente de novo.");
            }

            logger.LogWarning(
                "Regulação PR-{Numero}: Gravar do médico {Nome} no {Sistema} SEM confirmação (mensagem: {Mensagem}).",
                dados.NumeroLocal, nome, sistema, mensagem);
            throw new ValidacaoException("ser.medico_incerto", CadastroIncerto(sistema, nome,
                mensagem.Length > 0 ? $"O {sistema} disse: \"{mensagem}\"." : $"O {sistema} não disse nada."));
        }
        catch (Exception ex) when (ex is not ValidacaoException || !motor.EscritaAcionada)
        {
            if (!motor.EscritaAcionada)
            {
                // Nada saiu: volta a pendente, e o regulador pode tentar de novo.
                await medicosPendentes.LiberarCadastroAsync(pendenteId, CancellationToken.None);
                throw;
            }
            logger.LogWarning(ex, "Regulação PR-{Numero}: falha DEPOIS do Gravar do médico {Nome} no {Sistema}.",
                dados.NumeroLocal, nome, sistema);
            throw new ValidacaoException("ser.medico_incerto", CadastroIncerto(sistema, nome, $"Erro: {ex.Message}."));
        }
    }

    private static string CadastroIncerto(string sistema, string nome, string detalhe) =>
        $"ATENÇÃO: o Gravar chegou ao {sistema}, mas não deu para confirmar que \"{nome}\" entrou na lista de "
        + $"médicos. {detalhe} Confira no {sistema} antes de qualquer coisa: se o médico está lá, use \"Já existia\" "
        + "no cartão do médico e escolha o cadastro; se não está, \"Não entrou\". A plataforma não tenta de novo "
        + "sozinha — repetir pode duplicar o médico no cadastro do Estado.";

    public async Task<EnvioSerResultadoDto> EnviarAsync(
        Guid solicitacaoId, EnviarAoSerRequest req, CancellationToken ct)
    {
        var dados = await solicitacoes.PrepararEnvioAutomaticoAsync(solicitacaoId, ct);
        var ctx = Contextualizar(dados);
        var anexos = await LerAnexosAsync(ctx, dados, ct);

        // A crítica ANTES da trava: pedido parecido não é falha de envio, é pergunta.
        if (!req.EnviarMesmoComPedidoParecido)
        {
            var parecidos = await ProcurarParecidosAsync(ctx, dados, ct);
            if (parecidos.Count > 0)
            {
                throw new ConflitoException(
                    "ser.pedido_parecido",
                    $"O {ctx.Nome} já tem pedido deste paciente para \"{dados.RecursoRotulo}\" "
                    + $"({string.Join(", ", parecidos.Select(p => $"nº {p.IdSer}, {p.Situacao}"))}). "
                    + "Confira se não é o mesmo caso antes de enviar outro.");
            }
        }

        await solicitacoes.IniciarEnvioAutomaticoAsync(solicitacaoId, ct, dados.FormularioVersaoId);

        // Daqui em diante o envio vai até o fim mesmo que o navegador desista da espera (são umas
        // vinte idas ao sistema): cancelar no meio deixaria o caso preso em "Enviando" e, pior, sem
        // saber se o Gravar chegou. Quem fechou a tela vê o desfecho ao reabrir a solicitação.
        ct = CancellationToken.None;

        var motor = new SerCriacaoSolicitacao(ctx.Transporte, ctx.Perfil, logger);
        var gravarAcionado = false;
        try
        {
            var passos = await PreencherAsync(ctx, motor, dados, ct);

            foreach (var anexo in anexos)
            {
                await motor.AnexarAsync(anexo.Nome, anexo.ContentType, anexo.Conteudo, ct);
            }

            // Conferência pelo NOME, na coluna "Nome do Arquivo" da grade (08/10/2026). Contar linhas
            // deixou passar a PR-20 e a PR-22 com 2 linhas de nome VAZIO: o "Anexar" do modal cria a
            // linha mesmo quando o arquivo não chegou, e o download sai "Null". Linha sem nome é anexo
            // que não existe — e então NADA é gravado.
            var nomesNoSistema = motor.AnexosNomes();
            var faltando = anexos
                .Where(a => !nomesNoSistema.Any(n => string.Equals(n, a.Nome, StringComparison.OrdinalIgnoreCase)))
                .Select(a => a.Nome)
                .ToList();
            foreach (var anexo in anexos)
            {
                var entrou = !faltando.Contains(anexo.Nome);
                passos.Add(new EnvioSerPassoDto("Anexo", anexo.Nome, entrou,
                    entrou ? null : $"o {ctx.Nome} não registrou o arquivo"));
            }
            if (faltando.Count > 0)
            {
                logger.LogWarning(
                    "SER_ANEXO_NAO_RECEBIDO ({Sistema}): faltando [{Faltando}]; nomes na grade [{Nomes}]. {Diag}",
                    ctx.Nome, string.Join(" | ", faltando), string.Join(" | ", nomesNoSistema), Diagnostico(ctx, motor));
                throw new ValidacaoException(
                    "ser.anexo",
                    $"O {ctx.Nome} não registrou o arquivo {string.Join(", ", faltando.Select(n => $"\"{n}\""))} "
                    + "(a lista de anexos dele ficou sem o nome do arquivo). " + Diagnostico(ctx, motor) + " Nada foi gravado.");
            }

            gravarAcionado = true;
            var resposta = await motor.GravarAsync(
                $"criar solicitação PR-{dados.NumeroLocal} (Regulação) para o paciente", ct);
            var mensagem = SerCriacaoSolicitacao.MensagemDaResposta(resposta);
            var numero = SerCriacaoSolicitacao.NumeroGerado(resposta);

            if (numero is null)
            {
                // Mensagem de validação = o sistema recusou, nada foi criado. Sem mensagem nenhuma é o
                // único caso em que o pedido pode ter entrado sem a gente saber.
                var recusou = mensagem.Length > 0 && RegexRecusa().IsMatch(mensagem);
                if (recusou) gravarAcionado = false;
                throw new ValidacaoException(
                    "ser.gravar",
                    recusou
                        ? $"O {ctx.Nome} recusou o pedido: \"{mensagem}\". Nada foi criado lá."
                        : $"O {ctx.Nome} não devolveu o número da solicitação"
                          + (mensagem.Length > 0 ? $" (mensagem: \"{mensagem}\")" : string.Empty) + ".");
            }
            passos.Add(new EnvioSerPassoDto("Gravar", numero, true, mensagem));

            var conferido = await ConferirAsync(ctx, numero, dados, ct);
            passos.Add(new EnvioSerPassoDto($"Conferência no {ctx.Nome}", numero, conferido,
                conferido ? $"o pedido foi relido do {ctx.Nome} com este paciente e este recurso"
                          : $"o {ctx.Nome} deu o número, mas a releitura não achou o pedido na hora"));

            var detalhe = await solicitacoes.ConcluirEnvioAutomaticoAsync(
                solicitacaoId,
                new ConclusaoEnvioSer(numero, ctx.Operador, conferido, mensagem,
                    [.. anexos.SelectMany(a => a.Origens)]),
                ct);

            logger.LogInformation(
                "Regulação PR-{Numero}: enviada ao {Sistema} como {IdExterno} por {Operador} (conferida: {Conferido}).",
                dados.NumeroLocal, ctx.Nome, numero, ctx.Operador, conferido);

            return new EnvioSerResultadoDto(numero, conferido, mensagem, ctx.Operador, passos, detalhe, ctx.Nome);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            var motivo = ex is ValidacaoException or ConflitoException ? ex.Message : $"Erro inesperado: {ex.Message}";
            logger.LogWarning(ex, "Regulação PR-{Numero}: envio ao {Sistema} falhou (Gravar acionado: {Gravar}).",
                dados.NumeroLocal, ctx.Nome, gravarAcionado);
            // O contexto pode ter ficado com a solicitação alterada em memória (a conclusão falhou
            // ao salvar): limpa antes de registrar a falha, senão a transição sairia do estado errado.
            db.ChangeTracker.Clear();
            try
            {
                await solicitacoes.RegistrarFalhaEnvioAsync(solicitacaoId, motivo, gravarAcionado, CancellationToken.None);
            }
            catch (Exception registro)
            {
                logger.LogError(registro, "Regulação PR-{Numero}: não consegui registrar a falha do envio.", dados.NumeroLocal);
            }
            throw new ValidacaoException(
                "ser.envio",
                (gravarAcionado
                    ? $"ATENÇÃO: o Gravar chegou ao {ctx.Nome}. Confira lá se o pedido não foi criado antes de tentar de novo. "
                    : $"Nada foi gravado no {ctx.Nome}. ")
                + motivo + " A solicitação ficou como \"Falha no envio\".");
        }
    }

    [GeneratedRegex("obrigat|inv[aá]lid|n[aã]o (foi|pode|é)|informe|selecione|erro", RegexOptions.IgnoreCase)]
    private static partial Regex RegexRecusa();

    // ------------------------------------------------------------------ preenchimento

    /// <summary>
    /// Preenche a aba de criação na ordem que a tela impõe — (ramo, no SER) → tipo → recurso →
    /// paciente → bloco fixo → dinâmicos — e devolve o que foi posto em cada campo. Qualquer campo que
    /// o sistema não aceite é recusa aqui, com o nome do campo — nunca vai ao Gravar para ver no que dá.
    /// </summary>
    private async Task<List<EnvioSerPassoDto>> PreencherAsync(
        Contexto ctx, SerCriacaoSolicitacao motor, DadosEnvioSer dados, CancellationToken ct,
        MedicoNovoColetado? medicoNovo = null)
    {
        var passos = new List<EnvioSerPassoDto>();
        var traduzido = await TraduzirAsync(dados, ct);
        var sistema = ctx.Nome;

        await motor.AbrirAsync(ct);

        // ---- ramo (só o SER-RJ tem), tipo e recurso (pelo NOME)
        if (ctx.Perfil.TemRamo)
        {
            await motor.TrocarAsync(SerCriacaoSolicitacao.CampoSisReg, dados.AmbulatorioEstadual ? "true" : "false", ct);
        }
        var tipo = dados.EhExame ? "EXAME" : "CONSULTA";
        await motor.TrocarAsync(SerCriacaoSolicitacao.CampoTipo, tipo, ct);

        var recursos = IdentidadePorNome.Achar(motor.Combo(SerCriacaoSolicitacao.CampoRecurso), o => o.Rotulo, dados.RecursoRotulo);
        if (recursos.Count != 1)
        {
            throw new ValidacaoException(
                "ser.recurso",
                recursos.Count == 0
                    ? $"O {sistema} não oferece mais o recurso \"{dados.RecursoRotulo}\" ({tipo}"
                      + (ctx.Perfil.TemRamo ? $", ambulatório estadual: {(dados.AmbulatorioEstadual ? "Sim" : "Não")}" : string.Empty)
                      + "). Pode ter sido retirado ou renomeado — confira o pareamento do procedimento no catálogo da Regulação."
                    : $"O {sistema} tem {recursos.Count} recursos chamados \"{dados.RecursoRotulo}\" — não dá para escolher no chute.");
        }
        await motor.TrocarAsync(SerCriacaoSolicitacao.CampoRecurso, recursos[0].Valor, ct);
        // O SER-RJ tem também um autocomplete de recurso que precisa ser amarrado; o SERNIT não tem.
        if (motor.TemCampo(SerCriacaoSolicitacao.CampoRecursoSugestao))
        {
            var linhaRecurso = await motor.AmarrarAsync(
                SerCriacaoSolicitacao.CampoRecursoSugestao, recursos[0].Rotulo,
                linhas => Indice(linhas, l => l.Any(c => IdentidadePorNome.Chave(c) == IdentidadePorNome.Chave(recursos[0].Rotulo))),
                ct);
            if (linhaRecurso is null)
            {
                throw new ValidacaoException("ser.recurso", $"O autocomplete de recurso do {sistema} não sugeriu \"{recursos[0].Rotulo}\".");
            }
        }
        passos.Add(new EnvioSerPassoDto("Recurso", recursos[0].Rotulo, true,
            ctx.Perfil.TemRamo ? $"{tipo} · ambulatório estadual: {(dados.AmbulatorioEstadual ? "Sim" : "Não")}" : tipo));

        // ---- paciente
        var documento = Digitos(dados.PacienteCns) is { Length: 15 } cns ? cns
            : ctx.Perfil.PesquisaPorCpf && Digitos(dados.PacienteCpf) is { Length: 11 } cpf ? cpf
            : throw new ValidacaoException(
                "paciente",
                ctx.Perfil.PesquisaPorCpf
                    ? $"O paciente não tem CNS nem CPF — o {sistema} pesquisa por um dos dois."
                    : $"O {sistema} pesquisa paciente só pelo CNS, e o paciente não tem CNS no nosso cadastro. "
                      + "Complete o CNS antes de enviar.");
        await motor.PesquisarPacienteAsync(documento, ct);
        var paciente = motor.Paciente();
        var nomeNoSistema = paciente.FirstOrDefault(c => c.Campo.EndsWith(":nome", StringComparison.Ordinal))?.Valor
                            ?? paciente.FirstOrDefault(c => c.Rotulo.Equals("Nome", StringComparison.OrdinalIgnoreCase))?.Valor;
        var preenchidos = new HashSet<string>(StringComparer.Ordinal);
        var cadastradoNaTela = false;
        if (string.IsNullOrWhiteSpace(nomeNoSistema))
        {
            // O SERNIT não consulta o CADSUS: paciente que ele não conhece volta com o painel vazio e
            // aberto, e o regulador digitaria. Quem digita é a plataforma, com o nosso cadastro.
            if (!ctx.Perfil.CadastraPacienteNaTela || !motor.CampoEditavel(CampoNome))
            {
                throw new ValidacaoException(
                    "ser.paciente",
                    $"O {sistema} não achou paciente pelo {(documento.Length == 15 ? "CNS" : "CPF")} {documento}. "
                    + Diagnostico(ctx, motor));
            }
            nomeNoSistema = await CadastrarPacienteNaTelaAsync(ctx, motor, dados, paciente, passos, preenchidos, ct);
            cadastradoNaTela = true;
        }
        else if (IdentidadePorNome.Chave(nomeNoSistema) != IdentidadePorNome.Chave(dados.PacienteNome))
        {
            throw new ValidacaoException(
                "ser.paciente",
                $"O {(documento.Length == 15 ? "CNS" : "CPF")} {documento} é de \"{nomeNoSistema}\" no {sistema}, e a "
                + $"solicitação é de \"{dados.PacienteNome}\". Confira o cadastro antes de enviar — pedido no paciente "
                + "errado não se desfaz.");
        }

        // O SERNIT só grava com CPF (medido no lab, §5.4): sem CPF no cadastro de lá, o campo vem
        // aberto e vazio — vai o do nosso cadastro; sem CPF dos dois lados, não há como enviar.
        if (ctx.Perfil.CpfObrigatorio && !preenchidos.Contains(CampoCpf) && motor.CampoEditavel(CampoCpf)
            && string.IsNullOrWhiteSpace(motor.ValorNaPagina(CampoCpf)))
        {
            var nosso = Digitos(dados.PacienteCpf);
            if (nosso.Length != 11)
            {
                throw new ValidacaoException(
                    "ser.paciente",
                    $"O {sistema} exige CPF para gravar, e o paciente não tem CPF nem lá nem no nosso cadastro. "
                    + "Complete o CPF do paciente antes de enviar.");
            }
            var mascarado = $"{nosso[..3]}.{nosso[3..6]}.{nosso[6..9]}-{nosso[9..]}";
            motor.Digitar(CampoCpf, mascarado);
            preenchidos.Add(CampoCpf);
            passos.Add(new EnvioSerPassoDto("CPF", mascarado, true, $"o {sistema} não tinha o CPF; foi o do nosso cadastro"));
        }

        var faltandoNoCadastro = paciente
            .Where(c => c.Obrigatorio && c.Editavel && string.IsNullOrWhiteSpace(c.Valor) && !preenchidos.Contains(c.Campo))
            .Select(c => c.Rotulo).ToList();
        if (faltandoNoCadastro.Count > 0)
        {
            throw new ValidacaoException(
                "ser.paciente",
                $"O cadastro do paciente no {sistema} está sem: {string.Join(", ", faltandoNoCadastro)}. Complete lá e tente de novo.");
        }
        if (!cadastradoNaTela)
        {
            passos.Add(new EnvioSerPassoDto("Paciente", nomeNoSistema, true,
                $"achado pelo {(documento.Length == 15 ? "CNS" : "CPF")}"));
        }

        // ---- médico solicitante (pelo NOME; o value do combo é índice de view)
        await motor.TrocarAsync(SerCriacaoSolicitacao.RadioMedicoIdentificado,
            traduzido.GetValueOrDefault(SerCriacaoSolicitacao.RadioMedicoIdentificado, "true"), ct);
        if (dados.MedicoPendenteId is { } pendenteId && medicoNovo is not null)
        {
            // Médico pedido pela unidade e ainda fora da lista: a prévia segue (o regulador vê tudo
            // o que iria) e a decisão sobre o médico vem num bloco à parte.
            var bloco = await MedicoNovoAsync(ctx, motor, pendenteId, ct);
            medicoNovo.Bloco = bloco;
            passos.Add(new EnvioSerPassoDto("Médico solicitante", bloco.Nome, false,
                bloco.Situacao == SituacaoMedicoPendente.CadastroIncerto
                    ? $"houve uma tentativa de cadastrar no {sistema} sem confirmação — confira lá"
                    : $"não está na lista de médicos do {sistema} — decida abaixo"));
        }
        else
        {
            await PreencherMedicoAsync(ctx, motor, traduzido, passos, ct);
        }

        // ---- classificação de risco (o value é o nível: EMERGENCIA, URGENCIA…)
        var risco = Obrigatorio(traduzido, SerCriacaoSolicitacao.CampoRisco, "Classificação de risco");
        var riscos = motor.Combo(SerCriacaoSolicitacao.CampoRisco);
        var opcaoRisco = riscos.FirstOrDefault(o => o.Valor == risco)
            ?? throw new ValidacaoException("ser.risco", $"A classificação de risco \"{risco}\" não existe no {sistema}.");
        motor.Digitar(SerCriacaoSolicitacao.CampoRisco, opcaoRisco.Valor);
        passos.Add(new EnvioSerPassoDto("Classificação de risco", $"{opcaoRisco.Rotulo} ({opcaoRisco.Valor})", true));

        // ---- unidade de origem (decisão nossa, vem do mapa: "Não" + texto livre)
        var radioUnidade = traduzido.GetValueOrDefault(SerCriacaoSolicitacao.RadioUnidadeIdentificada, "false");
        await motor.TrocarAsync(SerCriacaoSolicitacao.RadioUnidadeIdentificada, radioUnidade, ct);
        if (radioUnidade == "false")
        {
            var unidade = Obrigatorio(traduzido, SerCriacaoSolicitacao.CampoUnidadeLivre, "Unidade de origem");
            if (!motor.TemCampo(SerCriacaoSolicitacao.CampoUnidadeLivre))
            {
                throw new ValidacaoException("ser.unidade", $"O {sistema} não abriu o campo de unidade de origem não identificada.");
            }
            motor.Digitar(SerCriacaoSolicitacao.CampoUnidadeLivre, unidade);
            passos.Add(new EnvioSerPassoDto("Unidade de origem", unidade, true, "não identificada (texto livre)"));
        }

        // ---- hipótese (CID): amarração + o texto que o sistema escreve no campo
        var hipotese = Obrigatorio(traduzido, SerCriacaoSolicitacao.CampoHipotese, "Hipótese (CID)");
        var codigo = RegexCodigoCid().Match(hipotese) is { Success: true } mc ? mc.Groups[1].Value : hipotese.Split(' ')[0];
        var linhaCid = await motor.AmarrarAsync(
            SerCriacaoSolicitacao.CampoHipotese, codigo,
            linhas => Indice(linhas, l => l.Count >= 2 && string.Equals(l[1].Trim(), codigo, StringComparison.OrdinalIgnoreCase)),
            ct)
            ?? throw new ValidacaoException(
                "ser.cid",
                $"O {sistema} não aceita o CID {codigo} para \"{dados.RecursoRotulo}\". Escolha outra hipótese da lista do recurso.");
        passos.Add(new EnvioSerPassoDto("Hipótese (CID)", linhaCid[0], true));

        // ---- campos dinâmicos do recurso (pelo RÓTULO)
        await PreencherDinamicosAsync(ctx, motor, dados, traduzido, passos, ct);

        return passos;
    }

    private async Task PreencherMedicoAsync(
        Contexto ctx, SerCriacaoSolicitacao motor, IReadOnlyDictionary<string, string> traduzido,
        List<EnvioSerPassoDto> passos, CancellationToken ct)
    {
        var sistema = ctx.Nome;
        var medico = Obrigatorio(traduzido, SerCriacaoSolicitacao.CampoMedico, "Médico solicitante");
        var medicos = IdentidadePorNome.Achar(motor.Combo(SerCriacaoSolicitacao.CampoMedico), o => o.Rotulo, medico);
        if (medicos.Count == 0)
        {
            throw new ValidacaoException(
                "ser.medico",
                $"O médico \"{medico}\" não está na lista de médicos do {sistema} do município. Ele precisa estar "
                + $"cadastrado e lotado no {sistema} antes do envio.");
        }
        // Homônimo no combo (cadastro duplicado): qualquer um leva o mesmo nome ao pedido.
        await motor.TrocarAsync(SerCriacaoSolicitacao.CampoMedico, medicos[0].Valor, ct);
        passos.Add(new EnvioSerPassoDto("Médico solicitante", medicos[0].Rotulo, true,
            medicos.Count > 1 ? $"{medicos.Count} cadastros com este nome no {sistema}" : null));
    }

    /// <summary>
    /// O bloco "médico não cadastrado" da prévia: o pedido da unidade, o que o combo de HOJE tem de
    /// parecido (e o CRM igual no espelho) e as especialidades do modal "Adicionar Médico".
    /// </summary>
    private async Task<MedicoNovoNoSistemaDto> MedicoNovoAsync(
        Contexto ctx, SerCriacaoSolicitacao motor, Guid pendenteId, CancellationToken ct)
    {
        var pendente = await medicosPendentes.ObterAsync(pendenteId, ct);
        var nomesNaLista = motor.Combo(SerCriacaoSolicitacao.CampoMedico).Select(o => o.Rotulo).ToList();
        var parecidos = await medicosPendentes.ParecidosAsync(
            pendente.Sistema, pendente.Nome, pendente.NumeroDocumento, ct, nomesNaLista);

        var modal = motor.ModalMedico();
        var especialidades = modal is null
            ? []
            : modal.Especialidades.Select(o => o.Rotulo.Trim()).Where(r => r.Length > 0)
                .DistinctBy(IdentidadePorNome.Chave).ToList();

        return new MedicoNovoNoSistemaDto(
            pendente.Id, pendente.Nome, pendente.TipoDocumento, pendente.NumeroDocumento, pendente.Especialidade,
            pendente.Situacao, parecidos, especialidades,
            SugerirEspecialidade(pendente.Especialidade, especialidades),
            PodeCadastrar: modal is not null && pendente.Situacao == SituacaoMedicoPendente.Pendente);
    }

    /// <summary>
    /// A especialidade da lista do sistema que parece a que a unidade escreveu. A unidade escreve o
    /// profissional ("ONCOLOGISTA", "PEDIATRA", "CLÍNICO GERAL"); a lista tem a especialidade
    /// ("ONCOLOGIA", "PEDIATRIA", "CLÍNICA GERAL"): casa pelo começo comum de cada palavra. Entre as que
    /// casam, a mais curta ("ONCOLOGIA" e não "ONCOLOGIA - MASTOLOGIA"). Só sugere: quem escolhe é o regulador.
    /// </summary>
    internal static string? SugerirEspecialidade(string? pedida, IReadOnlyList<string> lista)
    {
        var p = SemelhancaNome.Palavras(pedida);
        if (p.Count == 0) return null;

        static bool Casa(string a, string b)
        {
            var n = 0;
            while (n < a.Length && n < b.Length && a[n] == b[n]) n++;
            return n == a.Length || n == b.Length || n >= 6;
        }

        return lista
            .Select(rotulo => (Rotulo: rotulo, Palavras: SemelhancaNome.Palavras(rotulo)))
            .Where(c => c.Palavras.Count >= p.Count && p.Select((w, i) => Casa(w, c.Palavras[i])).All(x => x))
            .OrderBy(c => c.Palavras.Count)
            .ThenBy(c => c.Rotulo.Length)
            .Select(c => c.Rotulo)
            .FirstOrDefault();
    }

    private const string CampoCpf = "form0:cpf";
    private const string CampoNome = "form0:nome";
    private const string CampoUf = "form0:uf";
    private const string CampoMunicipio = "form0:municipio";

    /// <summary>
    /// Paciente que o sistema não conhece: o painel é digitado com o NOSSO cadastro, como o regulador
    /// faria (<see cref="PacienteNaTela"/>). Sem nome, CPF, sexo ou nascimento no nosso cadastro, para
    /// aqui — pedido com paciente incompleto não se corrige depois. UF e município vão por último: o
    /// município só existe depois do <c>onchange</c> da UF.
    /// </summary>
    private async Task<string> CadastrarPacienteNaTelaAsync(
        Contexto ctx, SerCriacaoSolicitacao motor, DadosEnvioSer dados, IReadOnlyList<SerCampoPacienteDto> painel,
        List<EnvioSerPassoDto> passos, HashSet<string> preenchidos, CancellationToken ct)
    {
        var sistema = ctx.Nome;
        var nosso = await pacientes.ObterPorIdAsync(dados.PacienteId, ct);
        var montagem = PacienteNaTela.Montar(nosso, painel);
        if (montagem.Faltando.Count > 0)
        {
            throw new ValidacaoException(
                "ser.paciente",
                $"O {sistema} não tem este paciente, e a plataforma o cadastra lá com os nossos dados — mas o "
                + $"nosso cadastro está sem: {string.Join(", ", montagem.Faltando)}. Complete o cadastro do "
                + "paciente e tente de novo.");
        }

        var nome = montagem.Campos.First(c => c.Campo == CampoNome).Valor;
        passos.Add(new EnvioSerPassoDto("Paciente", nome, true,
            $"o {sistema} não tem este paciente — vai cadastrado com os dados do nosso cadastro"
            + (montagem.Avisos.Count > 0 ? $" ({string.Join("; ", montagem.Avisos)})" : string.Empty)));
        foreach (var (campo, rotulo, valor) in montagem.Campos)
        {
            motor.Digitar(campo, valor);
            preenchidos.Add(campo);
            if (campo != CampoNome) passos.Add(new EnvioSerPassoDto($"Paciente · {rotulo}", valor, true));
        }

        // UF por extenso na tela; município pelo nome, depois do onchange da UF.
        if (nosso.Endereco is { } endereco && motor.CampoEditavel(CampoUf)
            && string.IsNullOrWhiteSpace(painel.FirstOrDefault(c => c.Campo == CampoUf)?.Valor))
        {
            var ufs = IdentidadePorNome.Achar(motor.Combo(CampoUf), o => o.Rotulo, PacienteNaTela.NomeDaUf(endereco.Uf));
            if (ufs.Count == 1)
            {
                await motor.TrocarAsync(CampoUf, ufs[0].Valor, ct);
                passos.Add(new EnvioSerPassoDto("Paciente · UF", ufs[0].Rotulo, true));
                var municipios = IdentidadePorNome.Achar(motor.Combo(CampoMunicipio), o => o.Rotulo, endereco.Cidade);
                if (municipios.Count == 1)
                {
                    motor.Digitar(CampoMunicipio, municipios[0].Valor);
                    passos.Add(new EnvioSerPassoDto("Paciente · Município", municipios[0].Rotulo, true));
                }
                else
                {
                    passos.Add(new EnvioSerPassoDto("Paciente · Município", endereco.Cidade, true,
                        $"o {sistema} não lista \"{endereco.Cidade}\" nessa UF — vai sem município"));
                }
            }
        }
        return nome;
    }

    private async Task PreencherDinamicosAsync(
        Contexto ctx, SerCriacaoSolicitacao motor, DadosEnvioSer dados, IReadOnlyDictionary<string, string> traduzido,
        List<EnvioSerPassoDto> passos, CancellationToken ct)
    {
        // Rótulo de cada campo dinâmico do espelho → o nome que o mapa usou. A tela de hoje pode
        // ter outro nome para o mesmo campo; o rótulo é o que a pessoa lê e o que se mantém.
        var doEspelho = dados.Sistema == SistemaRegulacao.Sernit
            ? await db.SernitCatalogoCampos.AsNoTracking().Where(c => c.RecursoId == dados.RecursoId)
                .Select(c => new { c.Campo, c.Rotulo }).ToListAsync(ct)
            : await db.SerCatalogoCampos.AsNoTracking().Where(c => c.RecursoId == dados.RecursoId)
                .Select(c => new { c.Campo, c.Rotulo }).ToListAsync(ct);
        var nomePorRotulo = doEspelho
            .GroupBy(c => IdentidadePorNome.Chave(c.Rotulo))
            .ToDictionary(g => g.Key, g => g.First().Campo, StringComparer.Ordinal);

        var faltando = new List<string>();
        foreach (var campo in motor.CamposDinamicos())
        {
            var valor = traduzido.GetValueOrDefault(campo.Campo);
            if (valor is null
                && nomePorRotulo.TryGetValue(IdentidadePorNome.Chave(campo.Rotulo), out var nomeAntigo))
            {
                valor = traduzido.GetValueOrDefault(nomeAntigo);
            }

            if (string.IsNullOrWhiteSpace(valor))
            {
                if (campo.Obrigatorio) faltando.Add(campo.Rotulo);
                continue;
            }

            if (campo.Opcoes is { Count: > 0 } opcoes)
            {
                var valores = valor.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (valores.Length > 1)
                {
                    throw new ValidacaoException(
                        "ser.campo",
                        $"O campo \"{campo.Rotulo}\" tem mais de uma escolha — o envio automático ainda não marca "
                        + $"várias opções. Lance este pedido pela tela do {ctx.Nome} e registre o número.");
                }
                var opcao = opcoes.FirstOrDefault(o => o.Valor == valor)
                            ?? opcoes.FirstOrDefault(o => IdentidadePorNome.Chave(o.Rotulo) == IdentidadePorNome.Chave(valor))
                            ?? throw new ValidacaoException(
                                "ser.campo", $"\"{valor}\" não é uma opção do campo \"{campo.Rotulo}\" no {ctx.Nome}.");
                valor = opcao.Valor;
            }

            motor.Digitar(campo.Campo, valor);
            passos.Add(new EnvioSerPassoDto(campo.Rotulo, valor, true));
        }

        if (faltando.Count > 0)
        {
            throw new ValidacaoException(
                "ser.campo",
                $"O {ctx.Nome} exige, para este recurso, campos que a solicitação não tem: {string.Join(", ", faltando)}. "
                + "Devolva à unidade para completar.");
        }
    }

    private async Task<IReadOnlyDictionary<string, string>> TraduzirAsync(DadosEnvioSer dados, CancellationToken ct)
    {
        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(dados.FormularioJson) ? "{}" : dados.FormularioJson);
        var canonico = doc.RootElement.TryGetProperty("canonico", out var c) ? c.Clone() : default;
        return await formularios.TraduzirAsync(dados.FormularioVersaoId, dados.Sistema, canonico, ct);
    }

    // ------------------------------------------------------------------ crítica e conferência

    /// <summary>
    /// Pedidos do mesmo paciente para o mesmo recurso que o sistema já tem em aberto — a crítica que o
    /// envio ao SISCAN também faz antes de gerar. Falha na consulta não bloqueia: vira lista vazia e
    /// fica no log (a crítica ajuda, não é a trava).
    /// </summary>
    private async Task<IReadOnlyList<EnvioSerDuplicadoDto>> ProcurarParecidosAsync(
        Contexto ctx, DadosEnvioSer dados, CancellationToken ct)
    {
        var cpf = Digitos(dados.PacienteCpf);
        var cns = Digitos(dados.PacienteCns);
        // Sem documento, a pesquisa viria sem filtro de paciente — a fila do município inteiro.
        if (cpf.Length != 11 && cns.Length != 15) return [];

        var linhas = await ctx.Pesquisar(
            cpf.Length == 11 ? new FiltroExterno(cpf, null, null) : new FiltroExterno(null, cns, null), ct);
        return [.. linhas
            .Where(l => IdentidadePorNome.Chave(l.Recurso) == IdentidadePorNome.Chave(dados.RecursoRotulo))
            .Select(l => new EnvioSerDuplicadoDto(l.Id, l.Recurso, l.DataSolicitacao, l.Situacao))];
    }

    /// <summary>"Salvo com sucesso" não é prova: relê o pedido pelo número e confere paciente e recurso.</summary>
    private async Task<bool> ConferirAsync(Contexto ctx, string numero, DadosEnvioSer dados, CancellationToken ct)
    {
        var linha = (await ctx.Pesquisar(new FiltroExterno(null, null, numero), ct)).FirstOrDefault(l => l.Id == numero);
        if (linha is null) return false;

        var mesmoPaciente = (Digitos(linha.Cns) is { Length: > 0 } cns && cns == Digitos(dados.PacienteCns))
                            || (Digitos(linha.Cpf) is { Length: > 0 } cpf && cpf == Digitos(dados.PacienteCpf))
                            || IdentidadePorNome.Chave(linha.Paciente) == IdentidadePorNome.Chave(dados.PacienteNome);
        var mesmoRecurso = string.IsNullOrWhiteSpace(linha.Recurso)
                           || IdentidadePorNome.Chave(linha.Recurso) == IdentidadePorNome.Chave(dados.RecursoRotulo);
        return mesmoPaciente && mesmoRecurso;
    }

    // ------------------------------------------------------------------ apoio

    /// <summary>A sessão do OPERADOR no sistema de destino, o transporte, o perfil de tela e a pesquisa.</summary>
    private Contexto Contextualizar(DadosEnvioSer dados)
    {
        var sessaoId = usuarioAtual.SessaoId
            ?? throw new ValidacaoException("ser.sem_operador", "O envio é assinado por quem está logado.");

        if (dados.Sistema == SistemaRegulacao.Sernit)
        {
            var sessao = sessoesSernit.Exigir(sessaoId);
            return new Contexto(
                "SERNIT", new TransporteSernit(sessao), PerfilTelaCriacao.Sernit,
                sessoesSernit.Estado(sessaoId).UsuarioSernit ?? "(operador)",
                async (f, ct) =>
                {
                    var leitor = new SernitLeitorService(sessao, loggerFactory.CreateLogger<SernitLeitorService>());
                    var saida = new List<LinhaExterna>();
                    foreach (var situacao in new[] { SituacaoSernit.EmFila, SituacaoSernit.Pendente, SituacaoSernit.Agendada })
                    {
                        try
                        {
                            var pagina = await leitor.PesquisarAsync(new SernitFiltroPesquisa
                            {
                                Situacao = situacao, Cpf = f.Cpf, Cns = f.Cns, IdSolicitacao = f.Id,
                            }, ct);
                            saida.AddRange(pagina.Linhas.Select(l => new LinhaExterna(
                                l.IdSernit, l.Recurso, l.Cpf, l.Cns, l.Paciente, l.DataSolicitacao, l.Situacao ?? situacao.ToString())));
                        }
                        catch (Exception ex) when (ex is not OperationCanceledException)
                        {
                            logger.LogWarning(ex, "SERNIT: pesquisa em {Situacao} falhou (crítica/releitura do envio).", situacao);
                        }
                    }
                    return saida;
                });
        }

        var sessaoSer = sessoesSer.Exigir(sessaoId);
        return new Contexto(
            "SER", new TransporteSer(sessaoSer), PerfilTelaCriacao.Ser,
            sessoesSer.Estado(sessaoId).UsuarioSer ?? "(operador)",
            async (f, ct) =>
            {
                var leitor = new SerLeitorService(sessaoSer, loggerFactory.CreateLogger<SerLeitorService>());
                var saida = new List<LinhaExterna>();
                foreach (var situacao in new[] { SituacaoSer.EmFila, SituacaoSer.Pendente, SituacaoSer.Agendada })
                {
                    try
                    {
                        var pagina = await leitor.PesquisarAsync(new SerFiltroPesquisa
                        {
                            Situacao = situacao, Cpf = f.Cpf, Cns = f.Cns, IdSolicitacao = f.Id,
                        }, ct);
                        saida.AddRange(pagina.Linhas.Select(l => new LinhaExterna(
                            l.IdSer, l.Recurso, l.Cpf, l.Cns, l.Paciente, l.DataSolicitacao, l.Situacao ?? situacao.ToString())));
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        logger.LogWarning(ex, "SER: pesquisa em {Situacao} falhou (crítica/releitura do envio).", situacao);
                    }
                }
                return saida;
            });
    }

    private async Task<IReadOnlyList<AnexoParaSer>> LerAnexosAsync(Contexto ctx, DadosEnvioSer dados, CancellationToken ct)
    {
        var lidos = new List<ArquivoLido>();
        foreach (var a in dados.Arquivos)
        {
            var conteudo = await arquivos.LerAsync(a.ChaveArmazenamento, ct)
                ?? throw new ValidacaoException("anexos", $"O anexo \"{a.Titulo ?? a.Nome}\" sumiu do armazenamento.");
            lidos.Add(new ArquivoLido(a.Id, a.Nome, a.Titulo, a.ContentType, conteudo));
        }
        if (lidos.Count == 0)
        {
            // O SER pergunta "foi inserido o pedido médico legível, datado e justificado?" quando não
            // há anexo — e não grava com "Não". O pedido médico é exigência dos dois sistemas.
            throw new ValidacaoException(
                "anexos", $"A solicitação não tem anexo. O {ctx.Nome} exige o pedido médico legível, datado e justificado.");
        }
        return AnexosParaSer.Preparar(lidos, ctx.Nome);
    }

    private static string Obrigatorio(IReadOnlyDictionary<string, string> traduzido, string campo, string rotulo) =>
        traduzido.GetValueOrDefault(campo) is { Length: > 0 } v
            ? v
            : throw new ValidacaoException("ser.campo", $"A solicitação está sem \"{rotulo}\".");

    private static int? Indice(IReadOnlyList<IReadOnlyList<string>> linhas, Func<IReadOnlyList<string>, bool> casa)
    {
        for (var i = 0; i < linhas.Count; i++) if (casa(linhas[i])) return i;
        return null;
    }

    private static string Digitos(string? s) => new([.. (s ?? string.Empty).Where(char.IsDigit)]);

    private static string Diagnostico(Contexto ctx, SerCriacaoSolicitacao motor) =>
        motor.Mensagem() is { Length: > 0 } m ? $"O {ctx.Nome} disse: \"{m}\"." : string.Empty;

    [GeneratedRegex(@"\(\s*([A-Z]\d{2,3}[A-Z0-9]*)\s*\)")]
    private static partial Regex RegexCodigoCid();
}
