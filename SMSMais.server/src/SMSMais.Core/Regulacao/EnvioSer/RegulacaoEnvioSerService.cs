using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Identidade;
using SMSMais.Core.Integracoes.SerWeb;
using SMSMais.Core.Integracoes.SerWeb.Varredura;
using SMSMais.Core.Regulacao.Anexos;
using SMSMais.Core.Regulacao.Catalogo;
using SMSMais.Core.Regulacao.Formularios;
using SMSMais.Core.Regulacao.Solicitacoes;
using SMSMais.Core.Ser.Criacao;
using SMSMais.Core.Ser.Dtos;
using SMSMais.Core.Ser.Sessao;
using SMSMais.Data;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.Ser;

namespace SMSMais.Core.Regulacao.EnvioSer;

/// <summary>Um passo do preenchimento: o que foi posto em qual campo do SER, e se o SER aceitou.</summary>
public sealed record EnvioSerPassoDto(string Campo, string? Valor, bool Ok, string? Observacao = null);

/// <summary>Um anexo como vai ao SER.</summary>
public sealed record EnvioSerAnexoDto(string Nome, long Tamanho, int ArquivosJuntados);

/// <summary>Pedido que o SER já tem para o mesmo paciente e o mesmo recurso.</summary>
public sealed record EnvioSerDuplicadoDto(string IdSer, string? Recurso, string? DataSolicitacao, string? Situacao);

/// <summary>A prévia: a tela do SER preenchida inteira, sem anexar nem gravar.</summary>
public sealed record EnvioSerPreparoDto(
    string OperadorSer,
    string Recurso,
    IReadOnlyList<EnvioSerPassoDto> Passos,
    IReadOnlyList<EnvioSerAnexoDto> Anexos,
    IReadOnlyList<EnvioSerDuplicadoDto> PossiveisDuplicados);

public sealed record EnviarAoSerRequest(bool EnviarMesmoComPedidoParecido);

/// <summary>O desfecho do envio — vai para o próprio modal, não para um toast.</summary>
public sealed record EnvioSerResultadoDto(
    string NumeroExterno,
    bool Conferido,
    string? MensagemDoSer,
    string OperadorSer,
    IReadOnlyList<EnvioSerPassoDto> Passos,
    RegulacaoSolicitacaoDetalheDto Solicitacao);

/// <summary>
/// Envia ao SER uma solicitação da Regulação — <b>a plataforma preenche tudo, anexa e grava</b>,
/// como o envio ao SISCAN na anamnese: primeiro a prévia (percorre a tela sem gravar), depois o
/// envio (grava e RELÊ do SER para provar).
///
/// <para><b>Quem assina é o regulador</b>, com o usuário e a senha DELE no SER (sessão em
/// memória, amarrada ao login no SMSMais — <see cref="ISerSessaoOperadorStore"/>). A credencial de
/// sincronismo nunca escreve: a trilha do SER grava o nome de quem fez.</para>
///
/// <para><b>Nada é achado por número guardado.</b> Recurso, médico e CID são escolhidos pelo NOME
/// na tela de hoje; os campos dinâmicos, pelo rótulo (07/10/2026: o número do recurso do SER
/// muda quando a SES acrescenta recursos).</para>
///
/// <para><b>Erros do SER voltam com o texto do SER.</b> Recusa antes do Gravar não cria nada; recusa
/// NO Gravar (mensagem de validação) também não; resposta sem número depois do Gravar é o único caso
/// em que o pedido PODE ter sido criado — e a solicitação fica em "Falha no envio" dizendo para
/// conferir no SER antes de tentar de novo.</para>
/// </summary>
public interface IRegulacaoEnvioSerService
{
    Task<EnvioSerPreparoDto> PrepararAsync(Guid solicitacaoId, CancellationToken ct);

    Task<EnvioSerResultadoDto> EnviarAsync(Guid solicitacaoId, EnviarAoSerRequest req, CancellationToken ct);
}

public sealed partial class RegulacaoEnvioSerService(
    SmsMaisDbContext db,
    IRegulacaoSolicitacaoService solicitacoes,
    IRegulacaoFormularioService formularios,
    IArquivoExigenciaStore arquivos,
    ISerSessaoOperadorStore sessoesSer,
    IUsuarioAtualAccessor usuarioAtual,
    ILoggerFactory loggerFactory,
    ILogger<RegulacaoEnvioSerService> logger) : IRegulacaoEnvioSerService
{
    public async Task<EnvioSerPreparoDto> PrepararAsync(Guid solicitacaoId, CancellationToken ct)
    {
        var dados = await solicitacoes.PrepararEnvioSerAsync(solicitacaoId, ct);
        var (sessao, operador) = SessaoDoOperador();
        var anexos = await LerAnexosAsync(dados, ct);

        var motor = new SerCriacaoSolicitacao(sessao, logger);
        var passos = await PreencherAsync(motor, dados, ct);
        var duplicados = await ProcurarParecidosAsync(sessao, dados, ct);

        return new EnvioSerPreparoDto(
            operador, dados.RecursoRotulo, passos,
            [.. anexos.Select(a => new EnvioSerAnexoDto(a.Nome, a.Conteudo.LongLength, a.Origens.Count))],
            duplicados);
    }

    public async Task<EnvioSerResultadoDto> EnviarAsync(
        Guid solicitacaoId, EnviarAoSerRequest req, CancellationToken ct)
    {
        var dados = await solicitacoes.PrepararEnvioSerAsync(solicitacaoId, ct);
        var (sessao, operador) = SessaoDoOperador();
        var anexos = await LerAnexosAsync(dados, ct);

        // A crítica ANTES da trava: pedido parecido no SER não é falha de envio, é pergunta.
        if (!req.EnviarMesmoComPedidoParecido)
        {
            var parecidos = await ProcurarParecidosAsync(sessao, dados, ct);
            if (parecidos.Count > 0)
            {
                throw new ConflitoException(
                    "ser.pedido_parecido",
                    $"O SER já tem pedido deste paciente para \"{dados.RecursoRotulo}\" "
                    + $"({string.Join(", ", parecidos.Select(p => $"nº {p.IdSer}, {p.Situacao}"))}). "
                    + "Confira se não é o mesmo caso antes de enviar outro.");
            }
        }

        await solicitacoes.IniciarEnvioAutomaticoAsync(solicitacaoId, ct);

        // Daqui em diante o envio vai até o fim mesmo que o navegador desista da espera (são umas
        // vinte idas ao SER): cancelar no meio deixaria o caso preso em "Enviando" e, pior, sem
        // saber se o Gravar chegou. Quem fechou a tela vê o desfecho ao reabrir a solicitação.
        ct = CancellationToken.None;

        var motor = new SerCriacaoSolicitacao(sessao, logger);
        var gravarAcionado = false;
        try
        {
            var passos = await PreencherAsync(motor, dados, ct);

            foreach (var anexo in anexos)
            {
                await motor.AnexarAsync(anexo.Nome, anexo.ContentType, anexo.Conteudo, ct);
            }
            var listados = motor.AnexosListados();
            foreach (var anexo in anexos)
            {
                var entrou = listados.Any(l => l.Contains(anexo.Nome, StringComparison.OrdinalIgnoreCase));
                passos.Add(new EnvioSerPassoDto("Anexo", anexo.Nome, entrou,
                    entrou ? null : "o SER não listou o arquivo depois de anexar"));
                if (!entrou)
                {
                    // DIAGNÓSTICO: o que o SER DE FATO listou na releitura — é o que diz se a grade
                    // veio vazia (A4J não atualizou), com nome diferente, ou sem o arquivo mesmo.
                    logger.LogWarning(
                        "SER_ANEXO_NAO_LISTADO: esperado \"{Esperado}\"; o SER listou {N} célula(s): [{Listados}].",
                        anexo.Nome, listados.Count, string.Join(" | ", listados));
                    throw new ValidacaoException(
                        "ser.anexo",
                        $"O SER não listou o anexo \"{anexo.Nome}\" depois de recebê-lo. "
                        + Diagnostico(motor) + " Nada foi gravado.");
                }
            }

            gravarAcionado = true;
            var resposta = await motor.GravarAsync(
                $"criar solicitação PR-{dados.NumeroLocal} (Regulação) para o paciente", ct);
            var mensagem = SerHtmlParser.MensagemDaTela(SerHtmlParser.Documento(resposta));
            var numero = SerCriacaoSolicitacao.NumeroGerado(resposta);

            if (numero is null)
            {
                // Mensagem de validação = o SER recusou, nada foi criado. Sem mensagem nenhuma é o
                // único caso em que o pedido pode ter entrado sem a gente saber.
                var recusou = mensagem.Length > 0 && RegexRecusa().IsMatch(mensagem);
                if (recusou) gravarAcionado = false;
                throw new ValidacaoException(
                    "ser.gravar",
                    recusou
                        ? $"O SER recusou o pedido: \"{mensagem}\". Nada foi criado lá."
                        : "O SER não devolveu o número da solicitação"
                          + (mensagem.Length > 0 ? $" (mensagem: \"{mensagem}\")" : string.Empty) + ".");
            }
            passos.Add(new EnvioSerPassoDto("Gravar", numero, true, mensagem));

            var conferido = await ConferirAsync(sessao, numero, dados, ct);
            passos.Add(new EnvioSerPassoDto("Conferência no SER", numero, conferido,
                conferido ? "o pedido foi relido do SER com este paciente e este recurso"
                          : "o SER deu o número, mas a releitura não achou o pedido na hora"));

            var detalhe = await solicitacoes.ConcluirEnvioAutomaticoAsync(
                solicitacaoId,
                new ConclusaoEnvioSer(numero, operador, conferido, mensagem,
                    [.. anexos.SelectMany(a => a.Origens)]),
                ct);

            logger.LogInformation(
                "Regulação PR-{Numero}: enviada ao SER como {IdSer} por {Operador} (conferida: {Conferido}).",
                dados.NumeroLocal, numero, operador, conferido);

            return new EnvioSerResultadoDto(numero, conferido, mensagem, operador, passos, detalhe);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            var motivo = ex is ValidacaoException or ConflitoException ? ex.Message : $"Erro inesperado: {ex.Message}";
            logger.LogWarning(ex, "Regulação PR-{Numero}: envio ao SER falhou (Gravar acionado: {Gravar}).",
                dados.NumeroLocal, gravarAcionado);
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
                    ? "ATENÇÃO: o Gravar chegou ao SER. Confira lá se o pedido não foi criado antes de tentar de novo. "
                    : "Nada foi gravado no SER. ")
                + motivo + " A solicitação ficou como \"Falha no envio\".");
        }
    }

    [GeneratedRegex("obrigat|inv[aá]lid|n[aã]o (foi|pode|é)|informe|selecione|erro", RegexOptions.IgnoreCase)]
    private static partial Regex RegexRecusa();

    // ------------------------------------------------------------------ preenchimento

    /// <summary>
    /// Preenche a aba de criação na ordem que a tela impõe — ramo → tipo → recurso → paciente →
    /// bloco fixo → dinâmicos — e devolve o que foi posto em cada campo. Qualquer campo que o SER
    /// não aceite é recusa aqui, com o nome do campo — nunca vai ao Gravar para ver no que dá.
    /// </summary>
    private async Task<List<EnvioSerPassoDto>> PreencherAsync(
        SerCriacaoSolicitacao motor, DadosEnvioSer dados, CancellationToken ct)
    {
        var passos = new List<EnvioSerPassoDto>();
        var traduzido = await TraduzirAsync(dados, ct);

        await motor.AbrirAsync(ct);

        // ---- ramo, tipo e recurso (pelo NOME)
        await motor.TrocarAsync(SerCriacaoSolicitacao.CampoSisReg, dados.AmbulatorioEstadual ? "true" : "false", ct);
        var tipo = dados.Tipo == TipoRecursoSer.Exame ? "EXAME" : "CONSULTA";
        await motor.TrocarAsync(SerCriacaoSolicitacao.CampoTipo, tipo, ct);

        var recursos = IdentidadePorNome.Achar(motor.Combo(SerCriacaoSolicitacao.CampoRecurso), o => o.Rotulo, dados.RecursoRotulo);
        if (recursos.Count != 1)
        {
            throw new ValidacaoException(
                "ser.recurso",
                recursos.Count == 0
                    ? $"O SER não oferece mais o recurso \"{dados.RecursoRotulo}\" ({tipo}, ambulatório estadual: "
                      + $"{(dados.AmbulatorioEstadual ? "Sim" : "Não")}). A SES pode tê-lo retirado ou renomeado — "
                      + "confira o pareamento do procedimento no catálogo da Regulação."
                    : $"O SER tem {recursos.Count} recursos chamados \"{dados.RecursoRotulo}\" — não dá para escolher no chute.");
        }
        await motor.TrocarAsync(SerCriacaoSolicitacao.CampoRecurso, recursos[0].Valor, ct);
        var linhaRecurso = await motor.AmarrarAsync(
            SerCriacaoSolicitacao.CampoRecursoSugestao, recursos[0].Rotulo,
            linhas => Indice(linhas, l => l.Any(c => IdentidadePorNome.Chave(c) == IdentidadePorNome.Chave(recursos[0].Rotulo))),
            ct);
        if (linhaRecurso is null)
        {
            throw new ValidacaoException("ser.recurso", $"O autocomplete de recurso do SER não sugeriu \"{recursos[0].Rotulo}\".");
        }
        passos.Add(new EnvioSerPassoDto("Recurso", recursos[0].Rotulo, true,
            $"{tipo} · ambulatório estadual: {(dados.AmbulatorioEstadual ? "Sim" : "Não")}"));

        // ---- paciente
        var documento = Digitos(dados.PacienteCns) is { Length: 15 } cns ? cns
            : Digitos(dados.PacienteCpf) is { Length: 11 } cpf ? cpf
            : throw new ValidacaoException("paciente", "O paciente não tem CNS nem CPF — o SER pesquisa por um dos dois.");
        await motor.PesquisarPacienteAsync(documento, ct);
        var paciente = motor.Paciente();
        var nomeNoSer = paciente.FirstOrDefault(c => c.Campo.EndsWith(":nome", StringComparison.Ordinal))?.Valor
                        ?? paciente.FirstOrDefault(c => c.Rotulo.Equals("Nome", StringComparison.OrdinalIgnoreCase))?.Valor;
        if (string.IsNullOrWhiteSpace(nomeNoSer))
        {
            throw new ValidacaoException(
                "ser.paciente",
                $"O SER não achou paciente pelo {(documento.Length == 15 ? "CNS" : "CPF")} {documento}. "
                + Diagnostico(motor));
        }
        if (IdentidadePorNome.Chave(nomeNoSer) != IdentidadePorNome.Chave(dados.PacienteNome))
        {
            throw new ValidacaoException(
                "ser.paciente",
                $"O {(documento.Length == 15 ? "CNS" : "CPF")} {documento} é de \"{nomeNoSer}\" no SER, e a solicitação "
                + $"é de \"{dados.PacienteNome}\". Confira o cadastro antes de enviar — pedido no paciente errado não se desfaz.");
        }
        var faltandoNoCadastro = paciente
            .Where(c => c.Obrigatorio && c.Editavel && string.IsNullOrWhiteSpace(c.Valor))
            .Select(c => c.Rotulo).ToList();
        if (faltandoNoCadastro.Count > 0)
        {
            throw new ValidacaoException(
                "ser.paciente",
                $"O cadastro do paciente no SER está sem: {string.Join(", ", faltandoNoCadastro)}. Complete no SER e tente de novo.");
        }
        passos.Add(new EnvioSerPassoDto("Paciente", nomeNoSer, true,
            $"achado pelo {(documento.Length == 15 ? "CNS" : "CPF")}"));

        // ---- médico solicitante (pelo NOME; o value do combo é índice de view)
        await motor.TrocarAsync(SerCriacaoSolicitacao.RadioMedicoIdentificado,
            traduzido.GetValueOrDefault(SerCriacaoSolicitacao.RadioMedicoIdentificado, "true"), ct);
        var medico = Obrigatorio(traduzido, SerCriacaoSolicitacao.CampoMedico, "Médico solicitante");
        var medicos = IdentidadePorNome.Achar(motor.Combo(SerCriacaoSolicitacao.CampoMedico), o => o.Rotulo, medico);
        if (medicos.Count == 0)
        {
            throw new ValidacaoException(
                "ser.medico",
                $"O médico \"{medico}\" não está na lista de médicos do SER do município. Ele precisa estar "
                + "cadastrado e lotado no SER antes do envio.");
        }
        // Homônimo no combo (cadastro duplicado no SER): qualquer um leva o mesmo nome ao pedido.
        await motor.TrocarAsync(SerCriacaoSolicitacao.CampoMedico, medicos[0].Valor, ct);
        passos.Add(new EnvioSerPassoDto("Médico solicitante", medicos[0].Rotulo, true,
            medicos.Count > 1 ? $"{medicos.Count} cadastros com este nome no SER" : null));

        // ---- classificação de risco (o value é o nível: EMERGENCIA, URGENCIA…)
        var risco = Obrigatorio(traduzido, SerCriacaoSolicitacao.CampoRisco, "Classificação de risco");
        var riscos = motor.Combo(SerCriacaoSolicitacao.CampoRisco);
        var opcaoRisco = riscos.FirstOrDefault(o => o.Valor == risco)
            ?? throw new ValidacaoException("ser.risco", $"A classificação de risco \"{risco}\" não existe no SER.");
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
                throw new ValidacaoException("ser.unidade", "O SER não abriu o campo de unidade de origem não identificada.");
            }
            motor.Digitar(SerCriacaoSolicitacao.CampoUnidadeLivre, unidade);
            passos.Add(new EnvioSerPassoDto("Unidade de origem", unidade, true, "não identificada (texto livre)"));
        }

        // ---- hipótese (CID): amarração + o texto que o SER escreve no campo
        var hipotese = Obrigatorio(traduzido, SerCriacaoSolicitacao.CampoHipotese, "Hipótese (CID)");
        var codigo = RegexCodigoCid().Match(hipotese) is { Success: true } mc ? mc.Groups[1].Value : hipotese.Split(' ')[0];
        var linhaCid = await motor.AmarrarAsync(
            SerCriacaoSolicitacao.CampoHipotese, codigo,
            linhas => Indice(linhas, l => l.Count >= 2 && string.Equals(l[1].Trim(), codigo, StringComparison.OrdinalIgnoreCase)),
            ct)
            ?? throw new ValidacaoException(
                "ser.cid",
                $"O SER não aceita o CID {codigo} para \"{dados.RecursoRotulo}\". Escolha outra hipótese da lista do recurso.");
        passos.Add(new EnvioSerPassoDto("Hipótese (CID)", linhaCid[0], true));

        // ---- campos dinâmicos do recurso (pelo RÓTULO)
        await PreencherDinamicosAsync(motor, dados, traduzido, passos, ct);

        return passos;
    }

    private async Task PreencherDinamicosAsync(
        SerCriacaoSolicitacao motor, DadosEnvioSer dados, IReadOnlyDictionary<string, string> traduzido,
        List<EnvioSerPassoDto> passos, CancellationToken ct)
    {
        // Rótulo de cada campo dinâmico do espelho → o nome que o mapa usou. A tela de hoje pode
        // ter outro nome para o mesmo campo; o rótulo é o que a pessoa lê e o que se mantém.
        var doEspelho = await db.SerCatalogoCampos.AsNoTracking()
            .Where(c => c.RecursoId == dados.SerRecursoId)
            .Select(c => new { c.Campo, c.Rotulo })
            .ToListAsync(ct);
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
                        + "várias opções. Lance este pedido pela tela do SER e registre o número.");
                }
                var opcao = opcoes.FirstOrDefault(o => o.Valor == valor)
                            ?? opcoes.FirstOrDefault(o => IdentidadePorNome.Chave(o.Rotulo) == IdentidadePorNome.Chave(valor))
                            ?? throw new ValidacaoException(
                                "ser.campo", $"\"{valor}\" não é uma opção do campo \"{campo.Rotulo}\" no SER.");
                valor = opcao.Valor;
            }

            motor.Digitar(campo.Campo, valor);
            passos.Add(new EnvioSerPassoDto(campo.Rotulo, valor, true));
        }

        if (faltando.Count > 0)
        {
            throw new ValidacaoException(
                "ser.campo",
                $"O SER exige, para este recurso, campos que a solicitação não tem: {string.Join(", ", faltando)}. "
                + "Devolva à unidade para completar.");
        }
    }

    private async Task<IReadOnlyDictionary<string, string>> TraduzirAsync(DadosEnvioSer dados, CancellationToken ct)
    {
        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(dados.FormularioJson) ? "{}" : dados.FormularioJson);
        var canonico = doc.RootElement.TryGetProperty("canonico", out var c) ? c.Clone() : default;
        return await formularios.TraduzirAsync(dados.FormularioVersaoId, SistemaRegulacao.Ser, canonico, ct);
    }

    // ------------------------------------------------------------------ crítica e conferência

    /// <summary>
    /// Pedidos do mesmo paciente para o mesmo recurso que o SER já tem em aberto — a crítica que o
    /// envio ao SISCAN também faz antes de gerar. Falha na consulta não bloqueia: vira lista vazia
    /// e fica no log (a crítica ajuda, não é a trava).
    /// </summary>
    private async Task<IReadOnlyList<EnvioSerDuplicadoDto>> ProcurarParecidosAsync(
        ISerWebSessao sessao, DadosEnvioSer dados, CancellationToken ct)
    {
        var cpf = Digitos(dados.PacienteCpf);
        var cns = Digitos(dados.PacienteCns);
        // Sem documento, a pesquisa viria sem filtro de paciente — a fila do município inteiro.
        if (cpf.Length != 11 && cns.Length != 15) return [];

        var leitor = new SerLeitorService(sessao, loggerFactory.CreateLogger<SerLeitorService>());
        var achados = new List<EnvioSerDuplicadoDto>();
        foreach (var situacao in new[] { SituacaoSer.EmFila, SituacaoSer.Pendente, SituacaoSer.Agendada })
        {
            try
            {
                var filtro = cpf.Length == 11
                    ? new SerFiltroPesquisa { Situacao = situacao, Cpf = cpf }
                    : new SerFiltroPesquisa { Situacao = situacao, Cns = cns };
                var pagina = await leitor.PesquisarAsync(filtro, ct);
                achados.AddRange(pagina.Linhas
                    .Where(l => IdentidadePorNome.Chave(l.Recurso) == IdentidadePorNome.Chave(dados.RecursoRotulo))
                    .Select(l => new EnvioSerDuplicadoDto(l.IdSer, l.Recurso, l.DataSolicitacao, l.Situacao ?? situacao.ToString())));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "SER: crítica de pedido parecido falhou em {Situacao}.", situacao);
            }
        }
        return achados;
    }

    /// <summary>"Salvo com sucesso" não é prova: relê o pedido pelo número e confere paciente e recurso.</summary>
    private async Task<bool> ConferirAsync(ISerWebSessao sessao, string numero, DadosEnvioSer dados, CancellationToken ct)
    {
        var leitor = new SerLeitorService(sessao, loggerFactory.CreateLogger<SerLeitorService>());
        foreach (var situacao in new[] { SituacaoSer.EmFila, SituacaoSer.Pendente, SituacaoSer.Agendada })
        {
            try
            {
                var pagina = await leitor.PesquisarAsync(
                    new SerFiltroPesquisa { Situacao = situacao, IdSolicitacao = numero }, ct);
                var linha = pagina.Linhas.FirstOrDefault(l => l.IdSer == numero);
                if (linha is null) continue;

                var mesmoPaciente = (Digitos(linha.Cns) is { Length: > 0 } cns && cns == Digitos(dados.PacienteCns))
                                    || (Digitos(linha.Cpf) is { Length: > 0 } cpf && cpf == Digitos(dados.PacienteCpf))
                                    || IdentidadePorNome.Chave(linha.Paciente) == IdentidadePorNome.Chave(dados.PacienteNome);
                var mesmoRecurso = string.IsNullOrWhiteSpace(linha.Recurso)
                                   || IdentidadePorNome.Chave(linha.Recurso) == IdentidadePorNome.Chave(dados.RecursoRotulo);
                return mesmoPaciente && mesmoRecurso;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "SER: releitura do pedido {Numero} em {Situacao} falhou.", numero, situacao);
            }
        }
        return false;
    }

    // ------------------------------------------------------------------ apoio

    private (ISerWebSessao Sessao, string Operador) SessaoDoOperador()
    {
        var sessaoId = usuarioAtual.SessaoId
            ?? throw new ValidacaoException("ser.sem_operador", "O envio ao SER é assinado por quem está logado.");
        var sessao = sessoesSer.Exigir(sessaoId);
        var operador = sessoesSer.Estado(sessaoId).UsuarioSer ?? "(operador)";
        return (sessao, operador);
    }

    private async Task<IReadOnlyList<AnexoParaSer>> LerAnexosAsync(DadosEnvioSer dados, CancellationToken ct)
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
            // O próprio SER pergunta "foi inserido o pedido médico legível, datado e justificado?"
            // quando não há anexo — e não grava com "Não".
            throw new ValidacaoException(
                "anexos", "A solicitação não tem anexo. O SER exige o pedido médico legível, datado e justificado.");
        }
        return AnexosParaSer.Preparar(lidos);
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

    private static string Diagnostico(SerCriacaoSolicitacao motor) =>
        motor.Mensagem() is { Length: > 0 } m ? $"O SER disse: \"{m}\"." : string.Empty;

    [GeneratedRegex(@"\(\s*([A-Z]\d{2,3}[A-Z0-9]*)\s*\)")]
    private static partial Regex RegexCodigoCid();
}
