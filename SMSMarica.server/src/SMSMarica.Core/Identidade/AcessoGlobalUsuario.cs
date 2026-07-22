using Microsoft.EntityFrameworkCore;
using SMSMarica.Data;

namespace SMSMarica.Core.Identidade;

/// <summary>
/// "Este usuário enxerga todas as unidades?" — resposta única para todo o sistema.
///
/// Antes de 2026-07-22 a pergunta era respondida comparando o id com o GUID do usuário
/// "Administrador" semeado (11111111-…), repetido em cada service que filtra por unidade.
/// Isso amarrava o acesso global a UMA conta compartilhada: com vários administradores, ou
/// todos entravam como o mesmo boneco (e a auditoria não distinguia ninguém), ou perdiam o
/// alcance. Agora é a coluna <c>usuario.acesso_global</c>.
///
/// A consulta vai ao banco (e não a uma claim do token) de propósito: revogar acesso global
/// tem que valer na hora, não no próximo login. É busca por chave primária — barata.
/// </summary>
public static class AcessoGlobalUsuario
{
    public static async Task<bool> TemAsync(
        SmsMaricaDbContext db, Guid? usuarioId, CancellationToken ct = default)
    {
        if (usuarioId is null) return false;
        return await db.Usuarios.AsNoTracking()
            .AnyAsync(u => u.Id == usuarioId && u.AcessoGlobal && u.Ativo && u.ExcluidoEm == null, ct);
    }
}
