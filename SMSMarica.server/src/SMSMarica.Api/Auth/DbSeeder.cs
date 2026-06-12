using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SMSMarica.Data;
using SMSMarica.Data.Entities;
using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Api.Auth;

/// <summary>
/// Seeds idempotentes executados no startup, depois das migrations.
/// Garante o Admin inicial (perfil + usuário com senha hash).
/// </summary>
public static class DbSeeder
{
    public static Guid AdminUsuarioId => IdentificadoresFixos.UsuarioAdminId;
    private const string AdminEmail = "admin@smsmarica.online";
    private const string AdminSenhaInicial = "Abc,123!";

    public static async Task SeedAsync(
        SmsMaricaDbContext db,
        IPasswordHasher<Usuario> hasher,
        CancellationToken cancellationToken = default)
    {
        await GarantirPerfilAdminAsync(db, cancellationToken);
        await GarantirUsuarioAdminAsync(db, hasher, cancellationToken);
        await GarantirTemplateMamografiaAsync(db, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Template "Mamografia Digital Bilateral (CDT)" — macro do CDT Maricá com as
    /// frases padronizadas por seção. Insert-only (não sobrescreve edições do
    /// usuário). Identificação do paciente e CRM/RQE do assinante NÃO entram no
    /// corpo: vêm do cabeçalho do laudo e da assinatura.
    /// </summary>
    private static async Task GarantirTemplateMamografiaAsync(SmsMaricaDbContext db, CancellationToken ct)
    {
        var existe = await db.LaudoTemplates
            .AnyAsync(t => t.Id == IdentificadoresFixos.TemplateMamografiaCdtId, ct);
        if (existe) return;

        db.LaudoTemplates.Add(new LaudoTemplate
        {
            Id = IdentificadoresFixos.TemplateMamografiaCdtId,
            Nome = "Mamografia Digital Bilateral (CDT)",
            Categoria = "Mamografia",
            Descricao = "Macro do CDT Maricá: selecione as frases aplicáveis e preencha os campos ____.",
            ConteudoHtml = TemplateMamografiaHtml,
            ConteudoJson = "{}",
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

    private static async Task GarantirPerfilAdminAsync(SmsMaricaDbContext db, CancellationToken ct)
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
        SmsMaricaDbContext db,
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
                Ativo = true,
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
