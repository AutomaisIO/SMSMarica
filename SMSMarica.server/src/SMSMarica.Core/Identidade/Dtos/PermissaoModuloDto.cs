using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Core.Identidade.Dtos;

/// <summary>Permissão sobre um módulo (par {Modulo, Acoes}). Acoes é int de flags.</summary>
public sealed record PermissaoModuloDto(ModuloPermissao Modulo, AcoesPermissao Acoes);
