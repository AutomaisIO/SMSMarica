using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Identidade;
using SMSMais.Core.Telefonia.Dtos;
using SMSMais.Data;
using SMSMais.Data.Entities.Telefonia;

namespace SMSMais.Core.Telefonia;

/// <summary>
/// Softphone dos usuários do painel. O ramal é criado e mantido no Automais.Pabx; aqui fica o
/// vínculo usuário ↔ ramal. A ordem é sempre Pabx primeiro, banco depois: se a telefonia
/// recusar, nada muda do lado de cá; se o banco falhar depois de criar no Pabx, o ramal novo é
/// desfeito lá.
/// </summary>
public interface ITelefoniaService
{
    Task<SoftphoneUsuarioDto> ObterDoUsuarioAsync(Guid usuarioId, CancellationToken ct = default);
    Task<SoftphoneUsuarioDto> DefinirAsync(Guid usuarioId, DefinirSoftphoneRequest request, CancellationToken ct = default);
    Task RemoverAsync(Guid usuarioId, CancellationToken ct = default);
    Task<RamaisLivresDto> SugerirRamaisAsync(CancellationToken ct = default);

    /// <summary>Credencial SIP do softphone do próprio usuário. Só existe para softphone ativo.</summary>
    Task<CredencialSoftphoneDto> ObterCredencialAsync(Guid usuarioId, CancellationToken ct = default);
}

public sealed class TelefoniaService(
    SmsMaisDbContext db,
    IPabxCliente pabx,
    IUsuarioAtualAccessor usuarioAtual,
    ILogger<TelefoniaService> logger) : ITelefoniaService
{
    public async Task<SoftphoneUsuarioDto> ObterDoUsuarioAsync(Guid usuarioId, CancellationToken ct = default)
    {
        var vinculo = await db.UsuarioSoftphones.AsNoTracking()
            .FirstOrDefaultAsync(s => s.UsuarioId == usuarioId, ct);
        return ParaDto(vinculo);
    }

    public async Task<SoftphoneUsuarioDto> DefinirAsync(Guid usuarioId, DefinirSoftphoneRequest request, CancellationToken ct = default)
    {
        var usuario = await db.Usuarios.AsNoTracking()
            .Where(u => u.Id == usuarioId && u.ExcluidoEm == null)
            .Select(u => new { u.Id, u.NomeCompleto })
            .FirstOrDefaultAsync(ct)
            ?? throw new NaoEncontradoException("Usuário", usuarioId);

        var ramal = request.Ramal.Trim();
        var nome = request.NomeExibicao.Trim();

        var outroDono = await db.UsuarioSoftphones.AsNoTracking()
            .AnyAsync(s => s.Ramal == ramal && s.UsuarioId != usuarioId, ct);
        if (outroDono)
            throw new ConflitoException("telefonia.ramal_em_uso", $"O ramal {ramal} já é o softphone de outro usuário.");

        var vinculo = await db.UsuarioSoftphones.FirstOrDefaultAsync(s => s.UsuarioId == usuarioId, ct);
        var descricao = $"Softphone SMSMais: {usuario.NomeCompleto}";
        var agora = DateTime.UtcNow;
        var quem = usuarioAtual.UsuarioId;

        if (vinculo is null)
        {
            await pabx.CriarSoftphoneAsync(ramal, nome, descricao, usuarioId, ct);
            if (!request.Ativo)
                await pabx.AtualizarSoftphoneAsync(ramal, nome, descricao, ativo: false, ct);

            vinculo = new UsuarioSoftphone
            {
                Id = Guid.NewGuid(),
                UsuarioId = usuarioId,
                Ramal = ramal,
                NomeExibicao = nome,
                Ativo = request.Ativo,
                CriadoEm = agora,
                CriadoPor = quem,
            };
            db.UsuarioSoftphones.Add(vinculo);
            await SalvarOuDesfazerNoPabxAsync(ramal, ct);
        }
        else if (vinculo.Ramal != ramal)
        {
            // Troca de número: cria o novo antes de soltar o antigo — se o novo for recusado,
            // o usuário continua com o que tinha.
            await pabx.CriarSoftphoneAsync(ramal, nome, descricao, usuarioId, ct);
            if (!request.Ativo)
                await pabx.AtualizarSoftphoneAsync(ramal, nome, descricao, ativo: false, ct);

            var antigo = vinculo.Ramal;
            vinculo.Ramal = ramal;
            vinculo.NomeExibicao = nome;
            vinculo.Ativo = request.Ativo;
            vinculo.AtualizadoEm = agora;
            vinculo.AtualizadoPor = quem;
            await SalvarOuDesfazerNoPabxAsync(ramal, ct);

            await pabx.ExcluirAsync(antigo, ct);
            logger.LogInformation("Softphone do usuário {UsuarioId} trocou do ramal {Antigo} para {Novo}", usuarioId, antigo, ramal);
        }
        else
        {
            await pabx.AtualizarSoftphoneAsync(ramal, nome, descricao, request.Ativo, ct);
            vinculo.NomeExibicao = nome;
            vinculo.Ativo = request.Ativo;
            vinculo.AtualizadoEm = agora;
            vinculo.AtualizadoPor = quem;
            await db.SaveChangesAsync(ct);
        }

        return ParaDto(vinculo);
    }

    public async Task RemoverAsync(Guid usuarioId, CancellationToken ct = default)
    {
        var vinculo = await db.UsuarioSoftphones.FirstOrDefaultAsync(s => s.UsuarioId == usuarioId, ct)
            ?? throw new NaoEncontradoException("Softphone do usuário", usuarioId);

        await pabx.ExcluirAsync(vinculo.Ramal, ct);
        db.UsuarioSoftphones.Remove(vinculo);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Softphone {Ramal} do usuário {UsuarioId} removido", vinculo.Ramal, usuarioId);
    }

    public Task<RamaisLivresDto> SugerirRamaisAsync(CancellationToken ct = default) =>
        pabx.SugerirLivresAsync(10, ct);

    public async Task<CredencialSoftphoneDto> ObterCredencialAsync(Guid usuarioId, CancellationToken ct = default)
    {
        var vinculo = await db.UsuarioSoftphones.AsNoTracking()
            .FirstOrDefaultAsync(s => s.UsuarioId == usuarioId && s.Ativo, ct)
            ?? throw new NaoEncontradoException("Softphone ativo do usuário", usuarioId);

        return await pabx.ObterCredencialAsync(vinculo.Ramal, ct);
    }

    private async Task SalvarOuDesfazerNoPabxAsync(string ramalCriado, CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch
        {
            // O Pabx já tem o ramal novo; sem o vínculo aqui ele ficaria órfão e reservado.
            try
            {
                await pabx.ExcluirAsync(ramalCriado, CancellationToken.None);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Ramal {Ramal} ficou órfão no Pabx: o vínculo não gravou e a exclusão de volta falhou.", ramalCriado);
            }
            throw;
        }
    }

    private static SoftphoneUsuarioDto ParaDto(UsuarioSoftphone? s) =>
        s is null
            ? new SoftphoneUsuarioDto(false, null, null, false)
            : new SoftphoneUsuarioDto(true, s.Ramal, s.NomeExibicao, s.Ativo);
}
