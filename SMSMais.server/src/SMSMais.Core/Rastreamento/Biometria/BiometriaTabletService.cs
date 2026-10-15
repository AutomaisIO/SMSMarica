using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Common.Tempo;
using SMSMais.Core.Notificacoes.WhatsApp;
using SMSMais.Data;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Rastreamento.Biometria;

/// <summary>
/// Lado do tablet do login pelo rosto (ADR-0073). Tudo pelo token do veículo: quem está escalado hoje,
/// a foto de referência deles, o primeiro acesso (CPF → código no WhatsApp → termo → foto), o registro
/// de cada conferência e a entrada pelo código da central.
/// </summary>
public interface IBiometriaTabletService
{
    /// <summary>Motoristas das rotas de hoje do veículo (e de ontem ainda em andamento).</summary>
    Task<IReadOnlyList<MotoristaEscaladoDto>> EscaladosDoDiaAsync(
        DispositivoVeiculo dispositivo, CancellationToken ct = default);

    /// <summary>Foto de referência — só de quem está escalado hoje no veículo do tablet.</summary>
    Task<ReferenciaFacialDto> ReferenciaAsync(
        DispositivoVeiculo dispositivo, Guid motoristaId, CancellationToken ct = default);

    Task<CodigoMotoristaEmitidoDto> SolicitarCodigoAsync(
        DispositivoVeiculo dispositivo, SolicitarCodigoMotoristaRequest request, CancellationToken ct = default);

    Task<PrimeiroAcessoLiberadoDto> ConfirmarCodigoAsync(
        DispositivoVeiculo dispositivo, ConfirmarCodigoMotoristaRequest request, CancellationToken ct = default);

    Task<ReferenciaCadastradaDto> CadastrarRostoAsync(
        DispositivoVeiculo dispositivo, CadastrarRostoRequest request, CancellationToken ct = default);

    /// <summary>Idempotente pelo <c>IdLocal</c> (a fila sem rede do tablet pode reenviar).</summary>
    Task<VerificacaoRegistradaDto> RegistrarVerificacaoAsync(
        DispositivoVeiculo dispositivo, RegistrarVerificacaoFacialRequest request, CancellationToken ct = default);

    Task<MotoristaLiberadoDto> UsarLiberacaoAsync(
        DispositivoVeiculo dispositivo, UsarLiberacaoRequest request, CancellationToken ct = default);
}

public sealed class BiometriaTabletService(
    SmsMaisDbContext db,
    IWhatsAppCliente whatsapp,
    IMemoryCache cache,
    IConfiguration config,
    ILogger<BiometriaTabletService> logger) : IBiometriaTabletService
{
    private static readonly TimeSpan ValidadeCodigo = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan IntervaloPedidos = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan ValidadeTicket = TimeSpan.FromMinutes(15);
    private const int MaxTentativasCodigo = 5;
    public const int MaxTentativasLiberacao = 5;

    // Foto do cadastro e selfie de conferência: o tablet manda JPEG reduzido (~50 KB).
    private const int TamanhoMaximoReferencia = 400_000;
    private const int TamanhoMaximoSelfie = 200_000;

    // ------------------------------------------------------------------ escala do dia

    public async Task<IReadOnlyList<MotoristaEscaladoDto>> EscaladosDoDiaAsync(
        DispositivoVeiculo dispositivo, CancellationToken ct = default)
    {
        var linhas = await (
            from r in RotasDoDia(dispositivo.VeiculoId)
            join m in MotoristasAtivos() on r.MotoristaId equals m.Id
            join f in db.ReferenciasFaciaisMotorista on m.Id equals f.MotoristaId into fs
            from f in fs.DefaultIfEmpty()
            orderby r.Data descending, r.CriadoEm
            select new
            {
                m.Id,
                m.Usuario.NomeCompleto,
                RotaId = r.Id,
                FotoHash = f == null ? null : f.FotoHash,
                CadastradaEm = f == null ? (DateTime?)null : f.CadastradaEm,
            }).ToListAsync(ct);

        // Duas rotas no dia com o mesmo motorista: aparece uma vez (a primeira do dia mais recente).
        return [.. linhas
            .GroupBy(l => l.Id)
            .Select(g => g.First())
            .Select(l => new MotoristaEscaladoDto(
                l.Id, l.NomeCompleto, l.RotaId, l.FotoHash is not null, l.FotoHash, l.CadastradaEm))];
    }

    public async Task<ReferenciaFacialDto> ReferenciaAsync(
        DispositivoVeiculo dispositivo, Guid motoristaId, CancellationToken ct = default)
    {
        var escalado = await RotasDoDia(dispositivo.VeiculoId).AnyAsync(r => r.MotoristaId == motoristaId, ct);
        var f = escalado
            ? await db.ReferenciasFaciaisMotorista.AsNoTracking().FirstOrDefaultAsync(x => x.MotoristaId == motoristaId, ct)
            : null;
        if (f is null) throw new NaoEncontradoException("Referência facial", motoristaId);
        return new ReferenciaFacialDto(f.MotoristaId, f.FotoBase64, f.FotoHash, f.CadastradaEm);
    }

    // ------------------------------------------------------------------ primeiro acesso

    public async Task<CodigoMotoristaEmitidoDto> SolicitarCodigoAsync(
        DispositivoVeiculo dispositivo, SolicitarCodigoMotoristaRequest request, CancellationToken ct = default)
    {
        var cpf = Digitos(request.Cpf);
        if (cpf.Length != 11) throw new ValidacaoException("cpf", "O CPF tem 11 números.");

        var alvo = await (
            from r in RotasDoDia(dispositivo.VeiculoId)
            join m in MotoristasAtivos() on r.MotoristaId equals m.Id
            where m.Usuario.Cpf == cpf
            select new { m.Id, m.Usuario.NomeCompleto, m.Usuario.Telefone }).FirstOrDefaultAsync(ct);
        if (alvo is null) return NadaEnviado(SituacaoPrimeiroAcesso.NaoEscalado);

        if (await db.ReferenciasFaciaisMotorista.AnyAsync(f => f.MotoristaId == alvo.Id, ct))
            return NadaEnviado(SituacaoPrimeiroAcesso.JaCadastrado);

        var telefone = Digitos(alvo.Telefone);
        if (telefone.Length < 10) return NadaEnviado(SituacaoPrimeiroAcesso.SemTelefone);

        if (cache.TryGetValue(ChaveIntervalo(cpf), out _))
            throw new ValidacaoException("codigo.aguarde", "Espere um minuto para pedir outro código.");

        var codigo = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        cache.Set(ChaveCodigo(cpf), new CodigoPendente(codigo, alvo.Id, alvo.NomeCompleto, dispositivo.Id), ValidadeCodigo);
        cache.Set(ChaveIntervalo(cpf), true, IntervaloPedidos);
        var validade = (int)ValidadeCodigo.TotalSeconds;
        var mascarado = "***-" + telefone[^4..];

        // Mesmo modo de teste e mesmo modelo de mensagem do login do cidadão (Tfd:Otp:*).
        if (config.GetValue("Tfd:Otp:ModoTeste", defaultValue: false))
        {
            logger.LogInformation("Primeiro acesso do motorista {MotoristaId}: código exibido na tela (modo teste).", alvo.Id);
            return new CodigoMotoristaEmitidoDto(true, "tela-teste", codigo, validade, mascarado, null);
        }

        var template = config.GetValue("Tfd:Otp:WhatsAppTemplate", "authzap")!;
        var idioma = config.GetValue("Tfd:Otp:WhatsAppIdioma", "pt_BR")!;
        var envio = await whatsapp.EnviarTemplateAutenticacaoAsync(
            telefone, template, idioma, codigo, ct: ct, origem: OrigemEnvioWhatsApp.Humano);

        // Sem credenciais do WhatsApp no servidor o cliente "simula": cai no código na tela.
        if (envio.Ok && (envio.WaMessageId?.StartsWith("simulado-", StringComparison.Ordinal) ?? false))
        {
            logger.LogWarning("WhatsApp não configurado: código do primeiro acesso exibido na tela.");
            return new CodigoMotoristaEmitidoDto(true, "tela-teste", codigo, validade, mascarado, null);
        }
        if (!envio.Ok)
        {
            cache.Remove(ChaveIntervalo(cpf));
            logger.LogWarning("Falha ao enviar o código do primeiro acesso do motorista {MotoristaId}: {Erro}",
                alvo.Id, envio.Erro);
            throw new ValidacaoException(
                "codigo.envio_falhou", "Não conseguimos mandar o código agora. Tente de novo ou peça à central.");
        }
        return new CodigoMotoristaEmitidoDto(true, "whatsapp", null, validade, mascarado, null);
    }

    public Task<PrimeiroAcessoLiberadoDto> ConfirmarCodigoAsync(
        DispositivoVeiculo dispositivo, ConfirmarCodigoMotoristaRequest request, CancellationToken ct = default)
    {
        var cpf = Digitos(request.Cpf);
        if (!cache.TryGetValue(ChaveCodigo(cpf), out CodigoPendente? pendente) || pendente is null
            || pendente.DispositivoId != dispositivo.Id)
        {
            throw new ValidacaoException("codigo.expirado", "O código venceu. Peça outro.");
        }

        if (pendente.Codigo != Digitos(request.Codigo))
        {
            pendente.Tentativas++;
            if (pendente.Tentativas >= MaxTentativasCodigo) cache.Remove(ChaveCodigo(cpf));
            throw new ValidacaoException("codigo.invalido", "Código errado. Confira e tente de novo.");
        }

        cache.Remove(ChaveCodigo(cpf));
        var ticket = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        cache.Set(ChaveTicket(ticket), new TicketCadastro(pendente.MotoristaId, dispositivo.Id), ValidadeTicket);

        return Task.FromResult(new PrimeiroAcessoLiberadoDto(
            ticket, pendente.MotoristaId, pendente.Nome,
            new TermoBiometriaDto(TermoBiometria.Versao, TermoBiometria.Texto)));
    }

    public async Task<ReferenciaCadastradaDto> CadastrarRostoAsync(
        DispositivoVeiculo dispositivo, CadastrarRostoRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(request.Ticket)
            || !cache.TryGetValue(ChaveTicket(request.Ticket), out TicketCadastro? ticket) || ticket is null
            || ticket.DispositivoId != dispositivo.Id)
        {
            throw new ValidacaoException("ticket", "O tempo do cadastro acabou. Comece de novo pelo CPF.");
        }
        if (request.TermoVersao != TermoBiometria.Versao)
            throw new ValidacaoException("termoVersao", "O termo mudou. Leia de novo e aceite.");

        var bytes = Jpeg(request.FotoBase64, TamanhoMaximoReferencia)
            ?? throw new ValidacaoException("fotoBase64", "A foto do rosto não veio ou não é uma imagem JPEG.");

        if (await db.ReferenciasFaciaisMotorista.AnyAsync(f => f.MotoristaId == ticket.MotoristaId, ct))
            throw new ConflitoException("biometria.ja_cadastrada", "O rosto deste motorista já está cadastrado.");

        var agora = DateTime.UtcNow;
        var hash = Convert.ToHexStringLower(SHA256.HashData(bytes));
        db.ReferenciasFaciaisMotorista.Add(new MotoristaReferenciaFacial
        {
            Id = Guid.NewGuid(),
            MotoristaId = ticket.MotoristaId,
            FotoBase64 = request.FotoBase64,
            FotoHash = hash,
            DispositivoId = dispositivo.Id,
            VeiculoId = dispositivo.VeiculoId,
            TermoVersao = TermoBiometria.Versao,
            TermoHash = TermoBiometria.Hash,
            CadastradaEm = agora,
        });

        // O cadastro também é uma conferência (código no WhatsApp + prova de vida): entra na trilha.
        var (lat, lon) = Posicao(request.Latitude, request.Longitude);
        db.VerificacoesFaciaisMotorista.Add(new VerificacaoFacialMotorista
        {
            Id = Guid.NewGuid(),
            IdLocal = Guid.NewGuid(),
            VeiculoId = dispositivo.VeiculoId,
            DispositivoId = dispositivo.Id,
            MotoristaId = ticket.MotoristaId,
            Momento = MomentoVerificacaoFacial.PrimeiroAcesso,
            Meio = MeioVerificacaoMotorista.Rosto,
            Aprovada = true,
            ProvaDeVida = true,
            Latitude = lat,
            Longitude = lon,
            CapturadaEm = agora,
            RecebidaEm = agora,
        });
        dispositivo.UltimoContatoEm = agora;

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Dois envios ao mesmo tempo: o índice único do motorista segurou o segundo.
            throw new ConflitoException("biometria.ja_cadastrada", "O rosto deste motorista já está cadastrado.");
        }
        cache.Remove(ChaveTicket(request.Ticket));
        return new ReferenciaCadastradaDto(ticket.MotoristaId, hash, agora);
    }

    // ------------------------------------------------------------------ conferências

    public async Task<VerificacaoRegistradaDto> RegistrarVerificacaoAsync(
        DispositivoVeiculo dispositivo, RegistrarVerificacaoFacialRequest request, CancellationToken ct = default)
    {
        if (request.IdLocal == Guid.Empty)
            throw new ValidacaoException("idLocal", "Identificador da conferência ausente.");
        if (!Enum.IsDefined(request.Momento))
            throw new ValidacaoException("momento", "Momento da conferência inválido.");

        var existente = await db.VerificacoesFaciaisMotorista.AsNoTracking()
            .FirstOrDefaultAsync(v => v.IdLocal == request.IdLocal, ct);
        if (existente is not null) return new VerificacaoRegistradaDto(existente.Id, existente.RecebidaEm);

        var agora = DateTime.UtcNow;
        var motoristaId = request.MotoristaId is { } m && await db.Motoristas.AnyAsync(x => x.Id == m, ct)
            ? m : (Guid?)null;
        var rotaId = request.RotaId is { } r && await db.Rotas.AnyAsync(x => x.Id == r, ct) ? r : (Guid?)null;
        var (lat, lon) = Posicao(request.Latitude, request.Longitude);
        // Selfie que não é JPEG ou grande demais não derruba o registro: só não é guardada.
        var selfie = Jpeg(request.SelfieBase64, TamanhoMaximoSelfie) is null ? null : request.SelfieBase64;

        var verificacao = new VerificacaoFacialMotorista
        {
            Id = Guid.NewGuid(),
            IdLocal = request.IdLocal,
            VeiculoId = dispositivo.VeiculoId,
            DispositivoId = dispositivo.Id,
            MotoristaId = motoristaId,
            RotaId = rotaId,
            Momento = request.Momento,
            Meio = MeioVerificacaoMotorista.Rosto,
            Aprovada = request.Aprovada && motoristaId is not null,
            Nota = request.Nota is >= -1 and <= 1 ? request.Nota : null,
            Limiar = request.Limiar is >= 0 and <= 1 ? request.Limiar : null,
            ProvaDeVida = request.ProvaDeVida,
            ModeloVersao = Cortar(request.ModeloVersao, 64),
            Latitude = lat,
            Longitude = lon,
            PrecisaoM = lat is not null && request.PrecisaoM is >= 0 ? request.PrecisaoM : null,
            CapturadaEm = HoraDoTablet(request.CapturadaEm, agora),
            RecebidaEm = agora,
            SelfieBase64 = selfie,
        };
        db.VerificacoesFaciaisMotorista.Add(verificacao);
        dispositivo.UltimoContatoEm = agora;

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            var corrida = await db.VerificacoesFaciaisMotorista.AsNoTracking()
                .FirstOrDefaultAsync(v => v.IdLocal == request.IdLocal, ct);
            if (corrida is null) throw;
            return new VerificacaoRegistradaDto(corrida.Id, corrida.RecebidaEm);
        }
        return new VerificacaoRegistradaDto(verificacao.Id, verificacao.RecebidaEm);
    }

    public async Task<MotoristaLiberadoDto> UsarLiberacaoAsync(
        DispositivoVeiculo dispositivo, UsarLiberacaoRequest request, CancellationToken ct = default)
    {
        // Reenvio depois de falha de rede: a liberação já foi usada por este pedido.
        if (request.IdLocal != Guid.Empty)
        {
            var ja = await (
                from v in db.VerificacoesFaciaisMotorista.AsNoTracking()
                where v.IdLocal == request.IdLocal && v.DispositivoId == dispositivo.Id
                    && v.Meio == MeioVerificacaoMotorista.CodigoCentral
                join m in db.Motoristas on v.MotoristaId equals (Guid?)m.Id
                select new MotoristaLiberadoDto(m.Id, m.Usuario.NomeCompleto)).FirstOrDefaultAsync(ct);
            if (ja is not null) return ja;
        }

        var codigo = Digitos(request.Codigo);
        if (codigo.Length != 6) throw new ValidacaoException("codigo", "O código da central tem 6 números.");

        var agora = DateTime.UtcNow;
        var liberacao = await db.LiberacoesMotorista
            .Where(l => l.VeiculoId == dispositivo.VeiculoId && l.UsadaEm == null && l.ExpiraEm > agora)
            .OrderByDescending(l => l.CriadaEm)
            .FirstOrDefaultAsync(ct);
        if (liberacao is null)
            throw new ValidacaoException("codigo.invalido", "Código errado ou vencido. Peça outro à central.");

        if (liberacao.CodigoHash != HashLiberacao(dispositivo.VeiculoId, codigo))
        {
            liberacao.TentativasErradas++;
            if (liberacao.TentativasErradas >= MaxTentativasLiberacao) liberacao.ExpiraEm = agora;
            await db.SaveChangesAsync(ct);
            throw new ValidacaoException("codigo.invalido", "Código errado ou vencido. Peça outro à central.");
        }

        var nome = await db.Motoristas.Where(m => m.Id == liberacao.MotoristaId)
            .Select(m => m.Usuario.NomeCompleto).FirstAsync(ct);
        liberacao.UsadaEm = agora;
        liberacao.UsadaPorDispositivoId = dispositivo.Id;

        var (lat, lon) = Posicao(request.Latitude, request.Longitude);
        db.VerificacoesFaciaisMotorista.Add(new VerificacaoFacialMotorista
        {
            Id = Guid.NewGuid(),
            IdLocal = request.IdLocal == Guid.Empty ? Guid.NewGuid() : request.IdLocal,
            VeiculoId = dispositivo.VeiculoId,
            DispositivoId = dispositivo.Id,
            MotoristaId = liberacao.MotoristaId,
            Momento = MomentoVerificacaoFacial.Login,
            Meio = MeioVerificacaoMotorista.CodigoCentral,
            Aprovada = true,
            Latitude = lat,
            Longitude = lon,
            CapturadaEm = agora,
            RecebidaEm = agora,
            LiberacaoId = liberacao.Id,
        });
        dispositivo.UltimoContatoEm = agora;
        await db.SaveChangesAsync(ct);
        return new MotoristaLiberadoDto(liberacao.MotoristaId, nome);
    }

    // ------------------------------------------------------------------ apoio

    /// <summary>Rotas do veículo hoje (fuso de Brasília), sem as canceladas, mais a de ontem que
    /// ainda está em andamento (volta que passa da meia-noite).</summary>
    private IQueryable<RotaDiaria> RotasDoDia(Guid veiculoId)
    {
        var hoje = FusoBrasilia.HojeEmBrasilia();
        var ontem = hoje.AddDays(-1);
        return db.Rotas.AsNoTracking().Where(r =>
            r.VeiculoId == veiculoId && r.Status != StatusRota.Cancelada
            && (r.Data == hoje || (r.Data == ontem && r.Status == StatusRota.EmAndamento)));
    }

    private IQueryable<Motorista> MotoristasAtivos() =>
        db.Motoristas.AsNoTracking().Where(m =>
            m.ExcluidoEm == null && m.Usuario.Ativo && m.Usuario.ExcluidoEm == null);

    internal static string HashLiberacao(Guid veiculoId, string codigo) =>
        Convert.ToHexStringLower(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes($"{veiculoId:N}:{codigo}")));

    /// <summary>Bytes do JPEG em base64, ou null se vazio, grande demais ou não for JPEG.</summary>
    internal static byte[]? Jpeg(string? base64, int tamanhoMaximo)
    {
        if (string.IsNullOrWhiteSpace(base64) || base64.Length > tamanhoMaximo) return null;
        try
        {
            var bytes = Convert.FromBase64String(base64);
            return bytes.Length > 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF ? bytes : null;
        }
        catch (FormatException)
        {
            return null;
        }
    }

    /// <summary>Coordenada fora do mundo ou 0,0 (GPS sem fix) vira "sem posição".</summary>
    private static (double? Lat, double? Lon) Posicao(double? lat, double? lon) =>
        lat is >= -90 and <= 90 && lon is >= -180 and <= 180 && !(lat == 0 && lon == 0) ? (lat, lon) : (null, null);

    /// <summary>Relógio do tablet adiantado não joga o registro para o futuro.</summary>
    private static DateTime HoraDoTablet(DateTime informada, DateTime agora)
    {
        if (informada == default) return agora;
        var utc = informada.Kind == DateTimeKind.Utc ? informada : DateTime.SpecifyKind(informada, DateTimeKind.Utc);
        return utc > agora + TimeSpan.FromMinutes(5) ? agora : utc;
    }

    private static string? Cortar(string? s, int max) =>
        string.IsNullOrWhiteSpace(s) ? null : (s.Trim().Length <= max ? s.Trim() : s.Trim()[..max]);

    private static string Digitos(string? v) =>
        string.IsNullOrEmpty(v) ? string.Empty : new string([.. v.Where(char.IsDigit)]);

    private static CodigoMotoristaEmitidoDto NadaEnviado(string situacao) =>
        new(false, "nenhum", null, 0, null, situacao);

    private static string ChaveCodigo(string cpf) => $"otp:motorista:{cpf}";
    private static string ChaveIntervalo(string cpf) => $"otp:motorista:intervalo:{cpf}";
    private static string ChaveTicket(string ticket) => $"biometria:ticket:{ticket}";

    private sealed class CodigoPendente(string codigo, Guid motoristaId, string nome, Guid dispositivoId)
    {
        public string Codigo { get; } = codigo;
        public Guid MotoristaId { get; } = motoristaId;
        public string Nome { get; } = nome;
        public Guid DispositivoId { get; } = dispositivoId;
        public int Tentativas { get; set; }
    }

    private sealed record TicketCadastro(Guid MotoristaId, Guid DispositivoId);
}
