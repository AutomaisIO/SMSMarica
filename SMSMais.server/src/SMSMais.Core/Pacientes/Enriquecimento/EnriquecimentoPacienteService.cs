using System.Globalization;
using Hl7.Fhir.Model;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using SMSMais.Core.Auditoria;
using SMSMais.Core.Common.Documentos;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Identidade;
using SMSMais.Core.Integracoes.Credenciais;
using SMSMais.Core.Integracoes.EsusPec;
using SMSMais.Core.Integracoes.Proxy;
using SMSMais.Core.Pacientes.Dtos;
using SMSMais.Core.Pacientes.Enriquecimento.Dtos;
using SMSMais.Core.Pacientes.Fhir;
using SMSMais.Core.Ser;
using SMSMais.Data;

namespace SMSMais.Core.Pacientes.Enriquecimento;

/// <summary>
/// "Enriquecer" a ficha do paciente com o cadastro de fora: o CADSUS (pela porta do SER — não gasta o
/// orçamento anti-robô do SISREG) ou o e-SUS PEC do município. Consulta, compara e grava SÓ o que a
/// pessoa escolheu na tela.
/// </summary>
public interface IEnriquecimentoPacienteService
{
    Task<ComparacaoFichaDto> ConsultarCadsusAsync(Guid pacienteId, CancellationToken ct = default);

    Task<ComparacaoFichaDto> ConsultarEsusAsync(Guid pacienteId, ConsultarEsusRequest request, CancellationToken ct = default);

    Task<ResultadoEnriquecimentoDto> AplicarAsync(Guid pacienteId, AplicarEnriquecimentoRequest request, CancellationToken ct = default);
}

/// <summary>
/// <para><b>Os valores ficam no servidor.</b> A consulta guarda a ficha da fonte por 20 minutos
/// (memória) e a tela devolve só QUAIS campos quer — o que se grava é o que a fonte disse, não o que
/// chegou no corpo da requisição. Ao gravar, a ficha é relida e recomparada: o que alguém mudou no
/// meio tempo não é atropelado.</para>
///
/// <para><b>Ordem de gravação:</b> todas as conferências (Receita, CPF de outro cadastro) acontecem
/// ANTES da primeira escrita — recusa nunca deixa a ficha pela metade.</para>
///
/// <para><b>e-SUS sem derrubar ninguém:</b> o PEC é sessão única por usuário. A conta da plataforma
/// (cedida por uma servidora, usada de madrugada pela rotina do ADR-0067) só serve a quem tem acesso
/// global e nunca força a entrada. A conta da pessoa só força se ELA aceitar encerrar a outra sessão.</para>
/// </summary>
public sealed class EnriquecimentoPacienteService(
    IPacienteFhirClient fhir,
    IPacientesService pacientes,
    ISerNovaSolicitacaoService ser,
    IConsultaFichaEsusPec esus,
    IIntegracaoCredencialService credenciais,
    IConsultaCpfService receita,
    IAuditoriaService auditoria,
    IUsuarioAtualAccessor usuarioAtual,
    SmsMaisDbContext db,
    IMemoryCache cache,
    ILogger<EnriquecimentoPacienteService> logger) : IEnriquecimentoPacienteService
{
    private static readonly TimeSpan Validade = TimeSpan.FromMinutes(20);

    private sealed record ConsultaGuardada(Guid PacienteId, Guid? UsuarioId, FichaExterna Ficha);

    public async Task<ComparacaoFichaDto> ConsultarCadsusAsync(Guid pacienteId, CancellationToken ct = default)
    {
        var (patient, ficha) = await LerAsync(pacienteId, ct);
        var (documento, por) = Documento(ficha);

        Ser.Dtos.SerPacienteEncontradoDto achado;
        try
        {
            achado = await ser.PesquisarPacienteAsync(documento, ct);
        }
        catch (Exception e) when (e is not (ValidacaoException or ConflitoException or OperationCanceledException))
        {
            logger.LogWarning(e, "Enriquecer pelo CADSUS: a pesquisa no SER falhou (paciente {Paciente}).", pacienteId);
            throw new ConflitoException(
                "enriquecimento.cadsus_indisponivel",
                "O SER não respondeu à consulta do CADSUS agora. Tente de novo em instantes.");
        }

        if (!achado.Encontrado) return NaoEncontrado(FichaExterna.Cadsus, por);
        return Montar(pacienteId, patient, ficha, FichaExterna.DoSer(achado.Campos, achado.Avisos), por);
    }

    public async Task<ComparacaoFichaDto> ConsultarEsusAsync(
        Guid pacienteId, ConsultarEsusRequest request, CancellationToken ct = default)
    {
        var (patient, ficha) = await LerAsync(pacienteId, ct);
        var (documento, por) = Documento(ficha);

        IntegracaoCredencialContexto? cred = null;
        try { cred = await credenciais.ObterContextoAsync(CorrecaoTelefoneEsusService.Provedor, ct); }
        catch (ValidacaoException) { /* não configurada: só a conta da pessoa serve */ }
        var parametros = CorrecaoTelefoneEsusService.Parametros.Ler(cred?.ParametrosJson);

        var daPlataforma = string.IsNullOrWhiteSpace(request.Senha);
        ContaEsusPec conta;
        if (daPlataforma)
        {
            if (!await AcessoGlobalUsuario.TemAsync(db, usuarioAtual.UsuarioId, ct))
            {
                throw new ValidacaoException("esus.senha_obrigatoria", "Informe o seu usuário e a sua senha do e-SUS.");
            }
            if (cred is not { Ativo: true } || string.IsNullOrWhiteSpace(cred.ClientId) || string.IsNullOrWhiteSpace(cred.ClientSecret))
            {
                throw new ConflitoException(
                    "esus.sem_credencial",
                    "A conta do e-SUS da plataforma não está configurada. Entre com a sua senha do e-SUS.");
            }
            conta = new ContaEsusPec(parametros.BaseUrl, cred.ClientId, cred.ClientSecret, Forcar: false, parametros.AcessoId);
        }
        else
        {
            if (string.IsNullOrWhiteSpace(request.Usuario))
            {
                throw new ValidacaoException("esus.usuario_obrigatorio", "Informe o usuário do e-SUS (o seu CPF).");
            }
            conta = new ContaEsusPec(parametros.BaseUrl, request.Usuario.Trim(), request.Senha!, request.EncerrarOutraSessao, null);
        }

        ResultadoConsultaEsusPec resultado;
        try
        {
            resultado = await esus.ConsultarAsync(conta, documento, ct);
        }
        catch (ErroEsusPec e) when (e.Message.StartsWith("Login:", StringComparison.Ordinal))
        {
            var motivo = e.Message["Login:".Length..].Trim();
            throw new ValidacaoException(
                "esus.login_recusado",
                daPlataforma
                    ? $"O e-SUS recusou a conta da plataforma: {motivo}"
                    : $"O e-SUS recusou o login: {motivo}");
        }
        catch (ErroEsusPec e)
        {
            logger.LogWarning("Enriquecer pelo e-SUS: {Erro} (paciente {Paciente}).", e.Message, pacienteId);
            throw new ConflitoException("esus.falhou", $"O e-SUS não completou a consulta: {e.Message}");
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            logger.LogWarning(e, "Enriquecer pelo e-SUS: sem resposta do PEC (paciente {Paciente}).", pacienteId);
            throw new ConflitoException("esus.indisponivel", "O e-SUS não respondeu agora. Tente de novo em instantes.");
        }

        if (resultado.OutraSessaoAberta)
        {
            throw daPlataforma
                ? new ConflitoException(
                    "esus.credencial_em_uso",
                    "A conta do e-SUS da plataforma está em uso agora, e não vamos derrubar quem está nela. Entre com a sua senha do e-SUS.")
                : new ConflitoException(
                    "esus.sessao_aberta",
                    "Você já está no e-SUS em outra janela ou computador. Para consultar daqui, aquela sessão será encerrada.");
        }

        if (resultado.Ficha is null) return NaoEncontrado(FichaExterna.Esus, por);
        return Montar(pacienteId, patient, ficha, FichaExterna.DoEsus(resultado.Ficha), por);
    }

    public async Task<ResultadoEnriquecimentoDto> AplicarAsync(
        Guid pacienteId, AplicarEnriquecimentoRequest request, CancellationToken ct = default)
    {
        if (!cache.TryGetValue<ConsultaGuardada>(Chave(request.ConsultaId), out var guardada)
            || guardada is null || guardada.PacienteId != pacienteId || guardada.UsuarioId != usuarioAtual.UsuarioId)
        {
            throw new ValidacaoException(
                "enriquecimento.consulta_expirada",
                "Esta consulta expirou. Clique de novo no botão para buscar os dados.");
        }

        var fonte = guardada.Ficha;
        var rotuloFonte = FichaExterna.Rotulo(fonte.Fonte);
        var (patient, ficha) = await LerAsync(pacienteId, ct);
        var comparacao = ComparadorFicha.Comparar(ficha, PacienteFhirMapper.TelefonesDe(patient), fonte);
        if (comparacao.Bloqueado)
        {
            throw new ConflitoException(
                "enriquecimento.cpf_divergente",
                $"O CPF no {rotuloFonte} é diferente do cadastro — pode ser outra pessoa. Nada foi gravado.");
        }

        var pedidos = (request.Campos ?? []).Distinct(StringComparer.Ordinal).ToList();
        var campos = comparacao.Campos.Where(c => pedidos.Contains(c.Campo)).ToDictionary(c => c.Campo);
        var jaIguais = pedidos.Where(p => !campos.ContainsKey(p)).ToList();
        var telefones = comparacao.Telefones
            .Where(t => (request.Telefones ?? []).Any(n => NormaFicha.MesmoNumero(n, t.Numero)))
            .ToList();

        if (campos.Count == 0 && telefones.Count == 0)
        {
            throw new ValidacaoException(
                "enriquecimento.nada_a_gravar",
                jaIguais.Count > 0
                    ? "O que foi escolhido já está igual na ficha — nada a gravar."
                    : "Escolha ao menos um dado para gravar.");
        }

        // ---- 1. conferências — antes de qualquer escrita
        var cpfNovo = campos.ContainsKey(ComparadorFicha.CampoCpf) ? fonte.Cpf : null;
        if (cpfNovo is not null)
        {
            var dono = await pacientes.ObterPorCpfAsync(cpfNovo, ct);
            if (dono is not null && dono.Id != pacienteId)
            {
                throw new ConflitoException(
                    "enriquecimento.cpf_de_outro_cadastro",
                    $"O CPF {NormaFicha.FormatarCpf(cpfNovo)} já é do cadastro de {dono.NomeCompleto}. Se for a mesma pessoa, use Unificar cadastros. Nada foi gravado.");
            }
        }

        var trocaNome = campos.ContainsKey(ComparadorFicha.CampoNome);
        var trocaNascimento = campos.ContainsKey(ComparadorFicha.CampoNascimento);
        if (trocaNome || trocaNascimento)
        {
            await ConferirNaReceitaAsync(
                cpfNovo ?? NormaFicha.Digitos(ficha.Cpf),
                trocaNascimento ? fonte.DataNascimento : ficha.DataNascimento,
                trocaNome ? fonte.Nome : null,
                rotuloFonte, ct);
        }

        // ---- 2. escritas (cada uma audita o antes/depois do seu campo)
        if (cpfNovo is not null) await pacientes.DefinirCpfAsync(pacienteId, cpfNovo, ct);
        if (campos.ContainsKey(ComparadorFicha.CampoCns)) await pacientes.TrocarCnsPrincipalAsync(pacienteId, fonte.Cns!, ct);
        if (trocaNascimento) await pacientes.CorrigirNascimentoAsync(pacienteId, fonte.DataNascimento!.Value, ct);
        if (trocaNome) await pacientes.AtualizarNomeAsync(pacienteId, new AtualizarNomePacienteRequest(fonte.Nome!), ct);

        string[] demais =
        [
            ComparadorFicha.CampoSexo, ComparadorFicha.CampoRaca, ComparadorFicha.CampoNomeSocial,
            ComparadorFicha.CampoMae, ComparadorFicha.CampoPai, ComparadorFicha.CampoEmail, ComparadorFicha.CampoEndereco,
        ];
        if (demais.Any(campos.ContainsKey))
        {
            // Pelo caminho da edição do painel: ele audita campo a campo e marca o grupo como editado
            // (o reimport do PEP não desfaz). Parte da ficha como ela está AGORA (já com CNS/nome novos).
            var atual = await pacientes.ObterPorIdAsync(pacienteId, ct);
            var req = ParaAtualizacao(atual);
            if (campos.ContainsKey(ComparadorFicha.CampoSexo)) req = req with { Sexo = fonte.Sexo!.Value };
            if (campos.ContainsKey(ComparadorFicha.CampoRaca)) req = req with { RacaCor = fonte.RacaCor!.Value };
            if (campos.ContainsKey(ComparadorFicha.CampoNomeSocial)) req = req with { NomeSocial = fonte.NomeSocial };
            if (campos.ContainsKey(ComparadorFicha.CampoMae)) req = req with { NomeDaMae = fonte.NomeMae };
            if (campos.ContainsKey(ComparadorFicha.CampoPai)) req = req with { NomeDoPai = fonte.NomePai };
            if (campos.ContainsKey(ComparadorFicha.CampoEmail)) req = req with { Email = fonte.Email };
            if (campos.ContainsKey(ComparadorFicha.CampoEndereco)) req = req with { Endereco = fonte.Endereco };
            await pacientes.AtualizarAsync(pacienteId, req, ct);
        }

        var acrescentados = new List<string>();
        foreach (var t in telefones)
        {
            var entrou = await pacientes.AdicionarTelefoneAsync(
                pacienteId, new AdicionarTelefoneRequest(t.Numero, t.Tipo, FichaExterna.OrigemTelefone(fonte.Fonte)), ct);
            if (entrou) acrescentados.Add(NormaFicha.FormatarTelefone(t.Numero));
        }

        var gravados = campos.Values.Select(c => c.Rotulo).ToList();
        await auditoria.RegistrarAsync(
            "Paciente", pacienteId.ToString(),
            fonte.Fonte == FichaExterna.Esus ? "EnriquecimentoEsusPec" : "EnriquecimentoCadsus",
            "",
            string.Join(" | ", new[]
            {
                gravados.Count > 0 ? "Campos: " + string.Join(", ", gravados) : null,
                acrescentados.Count > 0 ? "Telefones acrescentados: " + string.Join(", ", acrescentados) : null,
            }.Where(x => x is not null)),
            ct);

        cache.Remove(Chave(request.ConsultaId));
        logger.LogInformation(
            "Enriquecer pelo {Fonte}: paciente {Paciente} — {Campos} campo(s), {Telefones} telefone(s).",
            rotuloFonte, pacienteId, gravados.Count, acrescentados.Count);

        return new ResultadoEnriquecimentoDto(
            gravados, acrescentados,
            [.. jaIguais.Select(c => RotuloDoCampo(c))]);
    }

    // ------------------------------------------------------------------------------------------

    private async Task<(Patient Patient, PacienteDto Ficha)> LerAsync(Guid pacienteId, CancellationToken ct)
    {
        var patient = await fhir.ObterAsync(pacienteId, ct) ?? throw new NaoEncontradoException("Paciente", pacienteId);
        return (patient, PacienteFhirMapper.ParaDto(patient));
    }

    /// <summary>CPF primeiro — é a chave que une as bases; o CNS só quando não há CPF.</summary>
    private static (string Documento, string Por) Documento(PacienteDto ficha)
    {
        var cpf = NormaFicha.Digitos(ficha.Cpf);
        if (CpfBr.EhValido(cpf)) return (cpf, "CPF");
        var cns = NormaFicha.Digitos(ficha.Cns);
        if (cns.Length == 15) return (cns, "CNS");
        throw new ValidacaoException(
            "enriquecimento.sem_documento",
            "O paciente não tem CPF nem CNS no cadastro — não há por onde procurar.");
    }

    private ComparacaoFichaDto Montar(Guid pacienteId, Patient patient, PacienteDto ficha, FichaExterna fonte, string por)
    {
        var comparacao = ComparadorFicha.Comparar(ficha, PacienteFhirMapper.TelefonesDe(patient), fonte);

        Guid? consultaId = null;
        if (!comparacao.Bloqueado && (comparacao.Campos.Count > 0 || comparacao.Telefones.Count > 0))
        {
            consultaId = Guid.NewGuid();
            cache.Set(Chave(consultaId.Value), new ConsultaGuardada(pacienteId, usuarioAtual.UsuarioId, fonte), Validade);
        }

        return new ComparacaoFichaDto(
            consultaId, fonte.Fonte, FichaExterna.Rotulo(fonte.Fonte), true, por, fonte.AtualizadoEm,
            comparacao.Bloqueado, comparacao.Avisos, comparacao.Campos,
            [.. comparacao.Telefones.Select(t => new TelefoneSugeridoDto(t.Numero, t.Tipo, t.Rotulo))],
            comparacao.CamposIguais);
    }

    private static ComparacaoFichaDto NaoEncontrado(string fonte, string por) =>
        new(null, fonte, FichaExterna.Rotulo(fonte), false, por, null, false, [], [], [], 0);

    /// <summary>
    /// Regra de 02/10/2026: nome e nascimento divergentes se desempatam na Receita. O nascimento vai
    /// na própria consulta (CPF + data que não casam = recusa); o nome vem na resposta e é comparado.
    /// </summary>
    private async Task ConferirNaReceitaAsync(
        string cpf, DateOnly? nascimento, string? nome, string rotuloFonte, CancellationToken ct)
    {
        if (!CpfBr.EhValido(cpf))
        {
            throw new ValidacaoException(
                "enriquecimento.sem_cpf_para_receita",
                "Nome e data de nascimento só mudam com a conferência da Receita, que precisa do CPF — e este cadastro não tem. Nada foi gravado.");
        }
        if (nascimento is null)
        {
            throw new ValidacaoException(
                "enriquecimento.sem_nascimento_para_receita",
                "A conferência do nome na Receita precisa da data de nascimento, e o cadastro não tem. Nada foi gravado.");
        }

        var data = nascimento.Value.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
        Integracoes.Dtos.HubCpfRespostaDto resposta;
        try
        {
            resposta = await receita.ConsultarCpfAsync(cpf, nascimento.Value, ct);
        }
        catch (ValidacaoException)
        {
            throw new ValidacaoException(
                "enriquecimento.receita_nao_confere",
                $"A Receita não confirma o CPF {NormaFicha.FormatarCpf(cpf)} com o nascimento {data}. Nada foi gravado.");
        }
        catch (ConflitoException)
        {
            throw new ConflitoException(
                "enriquecimento.receita_indisponivel",
                "Não deu para conferir na Receita agora. Nada foi gravado — tente de novo em instantes.");
        }

        if (nome is not null && NormaFicha.Texto(resposta.Nome) != NormaFicha.Texto(nome))
        {
            throw new ValidacaoException(
                "enriquecimento.receita_nome_diferente",
                $"Na Receita o nome deste CPF é \"{resposta.Nome}\", diferente do {rotuloFonte} (\"{nome}\"). Nada foi gravado — o botão Verificar corrige o nome pela Receita.");
        }
    }

    private static string Chave(Guid consultaId) => $"enriquecimento:{consultaId:N}";

    private static string RotuloDoCampo(string campo) => campo switch
    {
        ComparadorFicha.CampoCpf => "CPF",
        ComparadorFicha.CampoCns => "CNS",
        ComparadorFicha.CampoNome => "Nome completo",
        ComparadorFicha.CampoNascimento => "Data de nascimento",
        ComparadorFicha.CampoSexo => "Sexo",
        ComparadorFicha.CampoRaca => "Raça/cor",
        ComparadorFicha.CampoNomeSocial => "Nome social",
        ComparadorFicha.CampoMae => "Nome da mãe",
        ComparadorFicha.CampoPai => "Nome do pai",
        ComparadorFicha.CampoEmail => "E-mail",
        ComparadorFicha.CampoEndereco => "Endereço",
        _ => campo,
    };

    /// <summary>A ficha atual como pedido de edição — todo o resto vai como está.</summary>
    internal static AtualizarPacienteRequest ParaAtualizacao(PacienteDto d) => new(
        d.Cns, d.Rg, d.Sexo, d.EstadoCivil, d.RacaCor, d.Escolaridade, d.Ocupacao, d.Naturalidade,
        d.Nacionalidade, d.NomeDaMae, d.NomeDoPai, d.ResponsavelLegal, d.Endereco, d.TelefonePrincipal,
        d.TelefoneCelular, d.TelefoneResidencial, d.Email, d.ContatoEmergencia, d.AlturaCm, d.PesoKg,
        d.TipoSanguineo, d.FatorRh, d.Alergias, d.MedicamentosContinuos, d.Comorbidades, d.Deficiencias,
        d.PlanoSaude, d.Observacoes, d.FotoBase64, d.NomeSocial);
}
