using System.Globalization;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SMSMais.Core.Cidadao.Dtos;
using SMSMais.Core.Cidadao.Entrada;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Common.Tempo;
using SMSMais.Core.Conversas;
using SMSMais.Core.Integracoes.Dtos;
using SMSMais.Core.Integracoes.EsusPec;
using SMSMais.Core.Integracoes.Proxy;
using SMSMais.Core.Notificacoes.WhatsApp;
using SMSMais.Core.Pacientes;
using SMSMais.Core.Pacientes.Dtos;
using SMSMais.Core.Pacientes.Fhir;
using SMSMais.Core.Sandbox;
using SMSMais.Core.Telefones;
using SMSMais.Data;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Cidadao;

public sealed class PacienteAuthService(
    SmsMaisDbContext db,
    IPacientesService pacientes,
    ICidadaoSessaoService sessoes,
    IWhatsAppCliente whatsapp,
    IMemoryCache cache,
    IConfiguration config,
    ITelefoneValidacaoService telefoneValidacao,
    IConsultaCpfService consultaCpf,
    IPersonificacaoPacienteService personificacoes,
    IPacienteFhirClient fhir,
    IConfiguracaoAppCidadaoService configuracaoApp,
    ITelefonesEsusNaEntrada telefonesEsus,
    ILogger<PacienteAuthService> logger) : IPacienteAuthService
{
    private static readonly TimeSpan Validade = TimeSpan.FromMinutes(5);
    private const int MaxTentativas = 5;

    private static readonly TimeSpan ValidadeEntrada = TimeSpan.FromMinutes(10);
    private const int EnviosPorEntrada = 3;
    internal const int FalhasAteBloquear = 5;
    internal static readonly TimeSpan JanelaDeFalhas = TimeSpan.FromMinutes(30);
    internal static readonly TimeSpan TempoBloqueado = TimeSpan.FromMinutes(30);

    internal const string OrigemCadastro = "cadastro";
    internal const string OrigemEsus = "esus";

    public async Task<EntradaCidadaoDto> IniciarEntradaAsync(
        IniciarEntradaRequest request, string? canal, CancellationToken ct = default)
    {
        var cpf = Digitos(request.Cpf);
        if (cpf.Length != 11) throw new ValidacaoException("cpf", "CPF deve ter 11 dígitos.");

        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
        var nascimento = request.DataNascimento;
        if (nascimento == default || nascimento > hoje || nascimento.Year < 1900)
            throw new ValidacaoException("nascimento.invalido", "Informe uma data de nascimento válida.");

        await GarantirNaoBloqueadoAsync(cpf, ct);

        var desafio = new DesafioEntrada(NovoIdDesafio(), cpf, nascimento, CanalAppCidadao.Normalizar(canal));
        var paciente = await pacientes.ObterPorCpfAsync(cpf, ct);

        if (paciente is null)
        {
            // Sem cadastro: quem confere o par é a Receita, que também dá o nome do cadastro a criar.
            var receita = await ConferirNaReceitaNaEntradaAsync(cpf, nascimento, ct);
            desafio.Cadastro = new CadastroPendente(receita.Nome.Trim(), nascimento, SexoDe(receita.Sexo));
        }
        else
        {
            if (!paciente.Ativo)
            {
                throw new ValidacaoException(
                    "paciente.inativo",
                    "Seu cadastro está inativo. Procure a sua unidade de saúde.");
            }

            var dados = await pacientes.ObterPorIdAsync(paciente.Id, ct);
            if (dados.DataNascimento is { } daFicha)
            {
                if (daFicha != nascimento) throw await NaoConfereAsync(cpf, ct);
            }
            else
            {
                // Ficha antiga sem data de nascimento: cai na mesma régua do cadastro novo.
                await ConferirNaReceitaNaEntradaAsync(cpf, nascimento, ct);
            }

            desafio.PacienteId = paciente.Id;
            desafio.Nome = paciente.NomeCompleto;

            // Já tem WhatsApp verificado: só ele. Oferecer os outros números da ficha seria trocar o
            // verificado sem passar pelo posto (ADR-0057 §5) — com CPF + nascimento, que não são segredo.
            if (!string.IsNullOrWhiteSpace(dados.TelefoneVerificado))
            {
                desafio.TemVerificado = true;
                desafio.Adicionar(TelefoneValidacaoService.Canonizar(dados.TelefoneVerificado), OrigemCadastro, verificado: true);
            }
            else
            {
                foreach (var numero in await CelularesDaFichaAsync(paciente.Id, ct))
                {
                    if (await NumeroLivreAsync(cpf, numero, ct)) desafio.Adicionar(numero, OrigemCadastro, verificado: false);
                }
            }
        }

        await ZerarFalhasAsync(cpf, ct);

        desafio.EsusPermitido = !desafio.TemVerificado
            && await configuracaoApp.ConsultarEsusNoCanalAsync(desafio.Canal, ct)
            && await telefonesEsus.DisponivelAsync(ct);
        cache.Set(ChaveEntrada(desafio.Id), desafio, ValidadeEntrada);
        return Dto(desafio);
    }

    public async Task<EntradaCidadaoDto> BuscarNoEsusAsync(string desafioId, CancellationToken ct = default)
    {
        var desafio = Desafio(desafioId);
        if (!desafio.EsusPermitido || desafio.EsusConsultado) return Dto(desafio);

        var numeros = await telefonesEsus.BuscarAsync(new PessoaNaEntrada(desafio.Cpf, desafio.Nascimento), ct);
        var patient = desafio.PacienteId is { } id ? await fhir.ObterAsync(id, ct) : null;
        foreach (var numero in numeros)
        {
            if (desafio.Opcoes.Any(o => TelefoneValidacaoService.EhMesmoNumero(o.Telefone, numero))) continue;
            if (patient is not null && TrocaTelefoneEsus.EhNumeroNegado(patient, numero)) continue;
            if (!await NumeroLivreAsync(desafio.Cpf, numero, ct)) continue;
            desafio.Adicionar(numero, OrigemEsus, verificado: false);
        }

        // Só depois de responder: "tente de novo em instantes" (conta em uso) precisa poder tentar de novo.
        desafio.EsusConsultado = true;
        logger.LogInformation(
            "Entrada do cidadão: e-SUS consultado para o CPF {Cpf} ({Novos} número(s) a mais).",
            desafio.Cpf, desafio.Opcoes.Count(o => o.Origem == OrigemEsus));
        return Dto(desafio);
    }

    public async Task<OtpEmitidoDto> EnviarCodigoEntradaAsync(
        string desafioId, EnviarCodigoEntradaRequest request, CancellationToken ct = default)
    {
        var desafio = Desafio(desafioId);
        var opcao = desafio.Opcoes.FirstOrDefault(o => o.Id == request.OpcaoId)
            ?? throw new ValidacaoException("entrada.opcao_invalida", "Escolha um dos números da lista.");

        if (desafio.Envios >= EnviosPorEntrada)
        {
            throw new ValidacaoException(
                "entrada.muitos_envios",
                "Você já pediu vários códigos. Espere alguns minutos e comece de novo.");
        }
        desafio.Envios++;

        // Barra ANTES de mandar o código: número que virou o contato confirmado de outra pessoa
        // nunca chegaria a ser validado, e queimar o código do cidadão por isso seria cruel.
        await telefoneValidacao.GarantirNumeroLivreAsync(desafio.Cpf, opcao.Telefone, ct);

        return await EmitirCodigoAsync(desafio.Cpf, opcao.Telefone, desafio.PacienteId, desafio.Nome, desafio.Cadastro, ct);
    }

    public async Task<RespostaLoginPacienteDto> ValidarOtpAsync(
        ValidarOtpRequest request, string? dispositivo, string? ip, CancellationToken ct = default)
    {
        var cpf = Digitos(request.Cpf);
        if (!cache.TryGetValue(Chave(cpf), out OtpEntry? entry) || entry is null)
        {
            throw new ValidacaoException("otp.expirado", "Código expirado ou inexistente. Solicite um novo código.");
        }

        if (entry.Codigo != Digitos(request.Codigo))
        {
            entry.Tentativas++;
            if (entry.Tentativas >= MaxTentativas) cache.Remove(Chave(cpf));
            throw new ValidacaoException("otp.invalido", "Código inválido. Confira e tente de novo.");
        }

        cache.Remove(Chave(cpf));

        // Cadastro novo só nasce AGORA: até aqui o cidadão só tinha afirmado um telefone; o
        // código recebido é a prova. Criar antes deixaria cadastro órfão a cada tentativa.
        var pacienteId = entry.PacienteId;
        var nome = entry.Nome;
        if (entry.Cadastro is { } novo)
        {
            (pacienteId, nome) = await CriarCadastroAsync(entry.Cpf, novo, entry.Telefone, ct);
        }

        if (pacienteId is not { } id || string.IsNullOrWhiteSpace(nome))
        {
            throw new ValidacaoException("otp.expirado", "Código expirado ou inexistente. Solicite um novo código.");
        }

        // O cidadão acabou de provar posse do número (recebeu o OTP no WhatsApp): marca
        // como contato validado DA PESSOA (CPF). Nunca quebra o login se falhar — exceto no
        // conflito real (número é o contato confirmado de outra pessoa), que precisa aparecer.
        try
        {
            await telefoneValidacao.MarcarValidadoAsync(entry.Cpf, entry.Telefone, "pwa-cidadao", null, ct);
        }
        catch (ConflitoException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Não foi possível marcar o telefone como validado no login do cidadão.");
        }

        // "Entrar como paciente" (Sandbox): quem acabou de provar o número é um operador que deixou
        // um paciente escolhido — a sessão abre como esse paciente. O código foi para o WhatsApp
        // DO OPERADOR (o fluxo acima não muda), então nada sai para o paciente.
        if (entry.Cadastro is null && await personificacoes.ResolverNoLoginAsync(entry.Cpf, ct) is { } alvo)
        {
            var (tokenAlvo, _) = await sessoes.AbrirSessaoPersonificadaAsync(
                alvo.PacienteId, alvo.Nome, alvo.Cpf, alvo.PersonificacaoId, alvo.ExpiraEm, dispositivo, ip, ct);
            return new RespostaLoginPacienteDto(tokenAlvo, new PacienteSessaoDto(alvo.PacienteId, alvo.Nome, alvo.Cpf));
        }

        // Abre a sessão single-device (revoga a anterior) e emite o token.
        var (token, _) = await sessoes.AbrirSessaoAsync(id, nome, entry.Cpf, "otp-whatsapp", dispositivo, ip, ct);

        return new RespostaLoginPacienteDto(token, new PacienteSessaoDto(id, nome, entry.Cpf));
    }

    /// <summary>
    /// Gera o código, guarda o que a validação vai precisar (paciente ou cadastro a criar +
    /// telefone a carimbar) e envia pelo WhatsApp. Sem WhatsApp configurado ou em modo teste,
    /// devolve o código na resposta para não travar o login.
    /// </summary>
    private async Task<OtpEmitidoDto> EmitirCodigoAsync(
        string cpf, string telefone, Guid? pacienteId, string? nome, CadastroPendente? cadastro, CancellationToken ct)
    {
        var codigo = GerarCodigo();
        cache.Set(Chave(cpf), new OtpEntry(codigo, pacienteId, nome, cpf, telefone, cadastro), Validade);
        var validadeSeg = (int)Validade.TotalSeconds;
        var mascarado = Mascarar(telefone);

        // Modo de teste explícito (dev): não envia, devolve o código para a tela.
        if (config.GetValue("Tfd:Otp:ModoTeste", defaultValue: false))
        {
            logger.LogInformation("OTP do paciente (CPF {Cpf}): {Codigo} — modo teste forçado.", cpf, codigo);
            return new OtpEmitidoDto(true, "tela-teste", codigo, validadeSeg, mascarado);
        }

        var template = config.GetValue("Tfd:Otp:WhatsAppTemplate", "authzap")!;
        var idioma = config.GetValue("Tfd:Otp:WhatsAppIdioma", "pt_BR")!;
        // O próprio cidadão pediu o código para entrar no app.
        var envio = await whatsapp.EnviarTemplateAutenticacaoAsync(
            telefone, template, idioma, codigo, pacienteId: pacienteId, ct: ct,
            origem: OrigemEnvioWhatsApp.Humano);

        // Sem credenciais salvas no servidor → o cliente "simula". Não trava o login:
        // cai no fallback de tela e registra aviso para configurar o WhatsApp.
        var simulado = envio.Ok && (envio.WaMessageId?.StartsWith("simulado-", StringComparison.Ordinal) ?? false);
        if (simulado)
        {
            logger.LogWarning(
                "WhatsApp não configurado (envio simulado): OTP exibido na tela como fallback. CPF {Cpf}.", cpf);
            return new OtpEmitidoDto(true, "tela-teste", codigo, validadeSeg, mascarado);
        }

        if (!envio.Ok)
        {
            logger.LogWarning("Falha ao enviar OTP por WhatsApp (CPF {Cpf}): {Erro}", cpf, envio.Erro);
            throw new ValidacaoException(
                "otp.envio_falhou",
                "Não conseguimos enviar seu código agora. Tente novamente em instantes.");
        }

        return new OtpEmitidoDto(true, "whatsapp", null, validadeSeg, mascarado);
    }

    /// <summary>Confere o par CPF + nascimento na Receita (proxy CPF) e devolve o nome oficial.</summary>
    private async Task<HubCpfRespostaDto> ConferirNaReceitaAsync(string cpf, DateOnly nascimento, CancellationToken ct)
    {
        HubCpfRespostaDto receita;
        try
        {
            receita = await consultaCpf.ConsultarCpfAsync(cpf, nascimento, ct);
        }
        catch (ValidacaoException ex)
        {
            // Negativa autoritativa do motor: o par não bate. Mensagem própria — a do proxy é
            // escrita para o operador do painel, não para o cidadão.
            logger.LogInformation("Login do cidadão: CPF {Cpf} não confere na Receita ({Msg}).", cpf, ex.Message);
            throw new ValidacaoException(
                "identidade.nao_confere",
                "O CPF e a data de nascimento não conferem. Confira os dados ou procure a sua unidade de saúde.");
        }

        if (string.IsNullOrWhiteSpace(receita.Nome))
        {
            throw new ValidacaoException(
                "identidade.sem_nome",
                "Não conseguimos confirmar seu nome agora. Tente novamente em instantes ou procure a sua unidade de saúde.");
        }

        return receita;
    }

    /// <summary>Cria o paciente do cidadão que se apresentou pelo app (nome oficial da Receita).</summary>
    private async Task<(Guid Id, string Nome)> CriarCadastroAsync(
        string cpf, CadastroPendente novo, string telefone, CancellationToken ct)
    {
        // O hub guarda a forma nacional (sem DDI).
        var nacional = telefone.Length > 11 ? telefone[^11..] : telefone;
        try
        {
            var id = await pacientes.CadastrarAsync(
                new CadastrarPacienteRequest(
                    NomeCompleto: novo.Nome,
                    Cpf: cpf,
                    DataNascimento: novo.DataNascimento,
                    Cns: null,
                    Rg: null,
                    Sexo: novo.Sexo,
                    TelefonePrincipal: nacional),
                ct);
            logger.LogInformation(
                "Paciente {Id} criado pelo autoatendimento do PWA (CPF conferido na Receita).", id);
            return (id, novo.Nome);
        }
        catch (ConflitoException)
        {
            // Corrida: alguém cadastrou este CPF entre o passo 2 e a confirmação do código.
            // O cidadão já provou identidade e telefone — reusa o cadastro em vez de barrar.
            var existente = await pacientes.ObterPorCpfAsync(cpf, ct);
            if (existente is null || !existente.Ativo) throw;
            logger.LogInformation("Cadastro do CPF {Cpf} já existia na confirmação do código — reusado.", cpf);
            return (existente.Id, existente.NomeCompleto);
        }
    }

    private static Sexo SexoDe(string? sexo) => sexo switch
    {
        "Masculino" => Sexo.Masculino,
        "Feminino" => Sexo.Feminino,
        _ => Sexo.NaoInformado,
    };

    private static string Chave(string cpf) => $"otp:paciente:{cpf}";

    private static string GerarCodigo() => RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");

    private static string Digitos(string? v) =>
        string.IsNullOrEmpty(v) ? string.Empty : new string([.. v.Where(char.IsDigit)]);

    /// <summary>Dica do destino para o usuário conferir, ex.: <c>***-1234</c>.</summary>
    private static string? Mascarar(string telefone)
    {
        var d = Digitos(telefone);
        return d.Length < 4 ? null : "***-" + d[^4..];
    }

    /// <summary>
    /// Confere CPF + nascimento na Receita, na entrada: a negativa conta como tentativa errada e volta
    /// com a mesma mensagem de qualquer "não confere".
    /// </summary>
    private async Task<HubCpfRespostaDto> ConferirNaReceitaNaEntradaAsync(string cpf, DateOnly nascimento, CancellationToken ct)
    {
        try
        {
            return await ConferirNaReceitaAsync(cpf, nascimento, ct);
        }
        catch (ValidacaoException ex) when (ex.Erros.ContainsKey("identidade.nao_confere"))
        {
            throw await NaoConfereAsync(cpf, ct);
        }
    }

    /// <summary>Os celulares ativos da ficha, na ordem do cadastro (o principal primeiro), sem os negados.</summary>
    private async Task<IReadOnlyList<string>> CelularesDaFichaAsync(Guid pacienteId, CancellationToken ct)
    {
        var patient = await fhir.ObterAsync(pacienteId, ct);
        if (patient is null) return [];

        var numeros = new List<string>();
        foreach (var t in TrocaTelefoneEsus.TelefonesAtivos(patient).OrderBy(t => t.Rank ?? int.MaxValue))
        {
            if (string.IsNullOrWhiteSpace(t.Value) || TrocaTelefoneEsus.EhNumeroNegado(patient, t.Value)) continue;
            var numero = TelefoneValidacaoService.Canonizar(t.Value);
            if (!TelefoneWhatsApp.EhCelularBr(numero)) continue;
            if (numeros.Any(n => TelefoneValidacaoService.EhMesmoNumero(n, numero))) continue;
            numeros.Add(numero);
        }
        return numeros;
    }

    /// <summary>Número que já é o contato confirmado de OUTRO CPF não entra na lista.</summary>
    private async Task<bool> NumeroLivreAsync(string cpf, string numero, CancellationToken ct)
    {
        try
        {
            await telefoneValidacao.GarantirNumeroLivreAsync(cpf, numero, ct);
            return true;
        }
        catch (ConflitoException)
        {
            return false;
        }
    }

    private async Task GarantirNaoBloqueadoAsync(string cpf, CancellationToken ct)
    {
        var bloqueio = await db.CidadaoEntradaBloqueios.AsNoTracking().FirstOrDefaultAsync(b => b.Cpf == cpf, ct);
        if (bloqueio?.BloqueadoAte is { } ate && ate > DateTime.UtcNow)
        {
            throw new ValidacaoException(
                "entrada.bloqueada",
                $"Muitas tentativas com este CPF. Tente de novo depois das {FusoBrasilia.ParaExibicao(ate):HH:mm} "
                + "ou procure a sua unidade de saúde.");
        }
    }

    /// <summary>
    /// Conta a tentativa errada e devolve a exceção — sempre a mesma mensagem, para não dizer a quem
    /// tenta adivinhar o que errou. <see cref="FalhasAteBloquear"/> erros em <see cref="JanelaDeFalhas"/>
    /// bloqueiam o CPF por <see cref="TempoBloqueado"/>.
    /// </summary>
    private async Task<ValidacaoException> NaoConfereAsync(string cpf, CancellationToken ct)
    {
        var agora = DateTime.UtcNow;
        try
        {
            var b = await db.CidadaoEntradaBloqueios.FirstOrDefaultAsync(x => x.Cpf == cpf, ct);
            if (b is null)
            {
                b = new CidadaoEntradaBloqueio { Cpf = cpf, PrimeiraFalhaEm = agora };
                db.CidadaoEntradaBloqueios.Add(b);
            }
            else if (agora - b.PrimeiraFalhaEm > JanelaDeFalhas || b.BloqueadoAte is { } ate && ate <= agora)
            {
                b.Falhas = 0;
                b.PrimeiraFalhaEm = agora;
                b.BloqueadoAte = null;
            }

            b.Falhas++;
            if (b.Falhas >= FalhasAteBloquear)
            {
                b.BloqueadoAte = agora + TempoBloqueado;
                logger.LogWarning("Entrada do cidadão: CPF {Cpf} bloqueado por {Minutos} min após {Falhas} erros.",
                    cpf, TempoBloqueado.TotalMinutes, b.Falhas);
            }
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            // Duas tentativas ao mesmo tempo no mesmo CPF: a outra já contou. Não derruba a resposta.
            logger.LogInformation(ex, "Entrada do cidadão: contagem de erro do CPF {Cpf} em corrida.", cpf);
        }

        return new ValidacaoException(
            "entrada.nao_confere",
            "Não conseguimos confirmar os seus dados. Confira o CPF e a data de nascimento ou procure a sua unidade de saúde.");
    }

    private Task ZerarFalhasAsync(string cpf, CancellationToken ct) =>
        db.CidadaoEntradaBloqueios.Where(b => b.Cpf == cpf).ExecuteDeleteAsync(ct);

    private DesafioEntrada Desafio(string? desafioId)
    {
        if (string.IsNullOrWhiteSpace(desafioId)
            || !cache.TryGetValue(ChaveEntrada(desafioId), out DesafioEntrada? desafio) || desafio is null)
        {
            throw new ValidacaoException("entrada.expirada", "O tempo para entrar acabou. Comece de novo.");
        }
        return desafio;
    }

    private static EntradaCidadaoDto Dto(DesafioEntrada d) => new(
        d.Id,
        [.. d.Opcoes.Select(o => new OpcaoEntradaDto(o.Id, TelefoneWhatsApp.Ultimos4(o.Telefone), o.Origem, o.Verificado))],
        d.EsusPermitido && !d.EsusConsultado,
        (int)ValidadeEntrada.TotalSeconds);

    private static string NovoIdDesafio() => Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();

    private static string ChaveEntrada(string desafioId) => $"entrada:desafio:{desafioId}";

    /// <summary>
    /// A entrada em andamento, entre "CPF + nascimento" e o envio do código. Guarda os números de verdade;
    /// o app só vê o final de cada um. Vive <see cref="ValidadeEntrada"/> no cache do servidor.
    /// </summary>
    private sealed class DesafioEntrada(string id, string cpf, DateOnly nascimento, string canal)
    {
        public string Id { get; } = id;
        public string Cpf { get; } = cpf;
        public DateOnly Nascimento { get; } = nascimento;
        public string Canal { get; } = canal;
        public Guid? PacienteId { get; set; }
        public string? Nome { get; set; }
        public CadastroPendente? Cadastro { get; set; }
        public bool TemVerificado { get; set; }
        public bool EsusPermitido { get; set; }
        public bool EsusConsultado { get; set; }
        public int Envios { get; set; }
        public List<OpcaoTelefone> Opcoes { get; } = [];

        public void Adicionar(string telefone, string origem, bool verificado) =>
            Opcoes.Add(new OpcaoTelefone((Opcoes.Count + 1).ToString(CultureInfo.InvariantCulture), telefone, origem, verificado));
    }

    private sealed record OpcaoTelefone(string Id, string Telefone, string Origem, bool Verificado);

    /// <summary>Cadastro a criar quando o código for confirmado (dados já conferidos na Receita).</summary>
    private sealed record CadastroPendente(string Nome, DateOnly DataNascimento, Sexo Sexo);

    private sealed class OtpEntry(
        string codigo, Guid? pacienteId, string? nome, string cpf, string telefone, CadastroPendente? cadastro)
    {
        public string Codigo { get; } = codigo;
        /// <summary>null quando o cadastro ainda vai ser criado (ver <see cref="Cadastro"/>).</summary>
        public Guid? PacienteId { get; } = pacienteId;
        public string? Nome { get; } = nome;
        public string Cpf { get; } = cpf;
        /// <summary>Número que recebeu o código — é ele que vira o contato verificado.</summary>
        public string Telefone { get; } = telefone;
        public CadastroPendente? Cadastro { get; } = cadastro;
        public int Tentativas { get; set; }
    }
}
