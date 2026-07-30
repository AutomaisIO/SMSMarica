using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SMSMarica.Core.Cidadao.Dtos;
using SMSMarica.Core.Common.Excecoes;
using SMSMarica.Core.Integracoes.Dtos;
using SMSMarica.Core.Integracoes.Proxy;
using SMSMarica.Core.Notificacoes.WhatsApp;
using SMSMarica.Core.Pacientes;
using SMSMarica.Core.Pacientes.Dtos;
using SMSMarica.Core.Telefones;
using SMSMarica.Data;
using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Core.Cidadao;

public sealed class PacienteAuthService(
    SmsMaricaDbContext db,
    IPacientesService pacientes,
    ICidadaoSessaoService sessoes,
    IWhatsAppCliente whatsapp,
    IMemoryCache cache,
    IConfiguration config,
    ITelefoneValidacaoService telefoneValidacao,
    IConsultaCpfService consultaCpf,
    ILogger<PacienteAuthService> logger) : IPacienteAuthService
{
    private static readonly TimeSpan Validade = TimeSpan.FromMinutes(5);
    private const int MaxTentativas = 5;

    public async Task<OtpEmitidoDto> SolicitarOtpAsync(SolicitarOtpRequest request, CancellationToken ct = default)
    {
        var cpf = Digitos(request.Cpf);
        if (cpf.Length != 11) throw new ValidacaoException("cpf", "CPF deve ter 11 dígitos.");

        var paciente = await pacientes.ObterPorCpfAsync(cpf, ct);

        // CPF sem cadastro: o app pede nascimento + telefone e o par é conferido na Receita
        // (o nº da solicitação não existe — não há paciente nosso a que ela pudesse pertencer).
        if (paciente is null) return NadaEnviado(SituacaoLoginCidadao.Cadastro);

        if (!paciente.Ativo)
        {
            throw new ValidacaoException(
                "paciente.inativo",
                "Seu cadastro está inativo. Procure a sua unidade de saúde.");
        }

        var dados = await pacientes.ObterPorIdAsync(paciente.Id, ct);

        // Sem contato VERIFICADO não sai código: mandar para um número que ninguém provou ser
        // desta pessoa é entregar o prontuário a quem estiver com o aparelho. A dica dos últimos
        // 4 dígitos ajuda quem ainda tem o número do cadastro a se reconhecer.
        if (string.IsNullOrWhiteSpace(dados.TelefoneVerificado))
        {
            var doCadastro = PrimeiroTelefone(
                dados.TelefoneCelular, dados.TelefonePrincipal, dados.TelefoneResidencial);
            return NadaEnviado(SituacaoLoginCidadao.Verificacao, doCadastro is null ? null : Mascarar(doCadastro));
        }

        var envio = await EmitirCodigoAsync(
            cpf, dados.TelefoneVerificado, paciente.Id, paciente.NomeCompleto, cadastro: null, ct);
        return envio;
    }

    public async Task<OtpEmitidoDto> SolicitarOtpVerificacaoAsync(
        SolicitarOtpVerificacaoRequest request, CancellationToken ct = default)
    {
        var cpf = Digitos(request.Cpf);
        if (cpf.Length != 11) throw new ValidacaoException("cpf", "CPF deve ter 11 dígitos.");

        var telefone = TelefoneValidacaoService.Canonizar(request.Telefone);
        // 55 (DDI) + DDD (2) + número (>=8) = 12 dígitos no mínimo.
        if (telefone.Length < 12)
            throw new ValidacaoException("telefone.invalido", "Informe um número de celular com DDD.");

        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
        if (request.DataNascimento == default
            || request.DataNascimento > hoje
            || request.DataNascimento.Year < 1900)
        {
            throw new ValidacaoException("nascimento.invalido", "Informe uma data de nascimento válida.");
        }

        var paciente = await pacientes.ObterPorCpfAsync(cpf, ct);

        Guid? pacienteId = null;
        string? nome = null;
        CadastroPendente? cadastro = null;

        if (paciente is null)
        {
            // Cadastro novo: quem confere a identidade é a Receita (par CPF + nascimento) — ela
            // também devolve o nome oficial, que é melhor do que qualquer nome digitado aqui.
            var receita = await ConferirNaReceitaAsync(cpf, request.DataNascimento, ct);
            cadastro = new CadastroPendente(receita.Nome.Trim(), request.DataNascimento, SexoDe(receita.Sexo));
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
            if (dados.DataNascimento is { } nascimento)
            {
                if (nascimento != request.DataNascimento)
                {
                    throw new ValidacaoException(
                        "nascimento.nao_confere",
                        "A data de nascimento não confere com o cadastro. Confira os dados ou procure a sua unidade de saúde.");
                }
            }
            else
            {
                // Cadastro antigo sem data de nascimento: cai na mesma régua do cadastro novo.
                await ConferirNaReceitaAsync(cpf, request.DataNascimento, ct);
            }

            await GarantirSolicitacaoAsync(paciente.Id, request.CodigoSolicitacao, ct);

            pacienteId = paciente.Id;
            nome = paciente.NomeCompleto;
        }

        // Barra ANTES de mandar o código: número que já é o contato confirmado de outra pessoa
        // nunca chegaria a ser validado, e queimar o código do cidadão por isso seria cruel.
        await telefoneValidacao.GarantirNumeroLivreAsync(cpf, telefone, ct);

        return await EmitirCodigoAsync(cpf, telefone, pacienteId, nome, cadastro, ct);
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
        var envio = await whatsapp.EnviarTemplateAutenticacaoAsync(
            telefone, template, idioma, codigo, pacienteId: pacienteId, ct: ct);

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

    /// <summary>
    /// Exige que o nº informado seja de uma solicitação DO PRÓPRIO paciente. É o que amarra o
    /// cidadão ao cadastro: quem não tem solicitação importada não entra por aqui (decisão de
    /// produto) e recebe a orientação de procurar o posto.
    /// </summary>
    private async Task GarantirSolicitacaoAsync(Guid pacienteId, string? codigoInformado, CancellationToken ct)
    {
        var codigo = Digitos(codigoInformado);
        if (codigo.Length == 0)
        {
            throw new ValidacaoException(
                "solicitacao.obrigatoria",
                "Informe o nº da solicitação do seu exame ou consulta.");
        }

        // O SISREG grava o número sem zeros à esquerda; quem digita costuma incluí-los.
        var semZeros = codigo.TrimStart('0');
        var existe = await db.Solicitacoes
            .AsNoTracking()
            .AnyAsync(
                s => s.ExcluidoEm == null
                     && s.PacienteId == pacienteId
                     && (s.CodigoSolicitacao == codigo || s.CodigoSolicitacao == semZeros),
                ct);

        if (!existe)
        {
            throw new ValidacaoException(
                "solicitacao.nao_encontrada",
                "Solicitação não encontrada. Entre em contato com o posto de atendimento.");
        }
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

    private static OtpEmitidoDto NadaEnviado(string situacao, string? telefoneMascarado = null) =>
        new(false, "nenhum", null, 0, telefoneMascarado, situacao);

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

    /// <summary>Primeiro telefone com pelo menos 10 dígitos (DDD + número), na ordem informada.</summary>
    private static string? PrimeiroTelefone(params string?[] candidatos) =>
        candidatos.FirstOrDefault(f => !string.IsNullOrWhiteSpace(f) && Digitos(f).Length >= 10);

    /// <summary>Dica do destino para o usuário conferir, ex.: <c>***-1234</c>.</summary>
    private static string? Mascarar(string telefone)
    {
        var d = Digitos(telefone);
        return d.Length < 4 ? null : "***-" + d[^4..];
    }

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
