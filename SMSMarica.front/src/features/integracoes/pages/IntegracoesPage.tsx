import { Link } from 'react-router-dom';
import { ChevronRight, KeySquare, Loader2, Settings2, Sparkles } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { useTemConsulta } from '@/shared/auth/authStore';
import { useCredenciais } from '@/features/integracoes/api';
import { CredencialOAuthCard } from '@/features/integracoes/components/CredencialOAuthCard';
import {
  DigitalOceanSpacesCard,
  PROVEDOR_SPACES,
} from '@/features/integracoes/components/DigitalOceanSpacesCard';
import { GoogleMapsCard } from '@/features/integracoes/components/GoogleMapsCard';
import { NavigationSdkCard } from '@/features/integracoes/components/NavigationSdkCard';
import { ProxyServicoSection } from '@/features/integracoes/components/ProxyServicoSection';
import { SisregCard, PROVEDOR_SISREG } from '@/features/integracoes/components/SisregCard';
import { WhatsAppCard } from '@/features/integracoes/components/WhatsAppCard';

function LinkCard({ to, titulo, descricao }: { to: string; titulo: string; descricao: string }) {
  return (
    <Link
      to={to}
      className="flex items-center justify-between gap-3 rounded-xl border border-gray-200 bg-white p-4 shadow-sm hover:border-primary-300 hover:bg-primary-50/30"
    >
      <span>
        <span className="block font-medium text-gray-900">{titulo}</span>
        <span className="block text-xs text-gray-500">{descricao}</span>
      </span>
      <ChevronRight className="h-4 w-4 shrink-0 text-gray-400" />
    </Link>
  );
}

export function IntegracoesPage() {
  const credenciais = useCredenciais();
  const podeVerIa = useTemConsulta('InteligenciaConfiguracao');
  const podeVerSisreg = useTemConsulta('SisregConfiguracao');
  const podeVerTokens = useTemConsulta('ApiTokens');
  const podeVerProxy = useTemConsulta('IntegracoesConfig');

  // O Spaces (S3) tem card próprio (Access/Secret Key + endpoint/region/bucket),
  // então é separado da lista genérica de provedores OAuth.
  const lista = credenciais.data ?? [];
  const credsOauth = lista.filter((c) => c.provedor !== PROVEDOR_SPACES && c.provedor !== PROVEDOR_SISREG);
  const credSpaces = lista.find((c) => c.provedor === PROVEDOR_SPACES) ?? {
    provedor: PROVEDOR_SPACES,
    rotulo: 'DigitalOcean Spaces (S3)',
    clientIdDefinido: false,
    clientSecretDefinido: false,
    redirectUri: null,
    parametrosJson: null,
    ativo: false,
  };
  const credSisreg = lista.find((c) => c.provedor === PROVEDOR_SISREG) ?? {
    provedor: PROVEDOR_SISREG,
    rotulo: 'SISREG (consulta de paciente por CNS)',
    clientIdDefinido: false,
    clientSecretDefinido: false,
    redirectUri: null,
    parametrosJson: null,
    ativo: false,
  };

  return (
    <div className="space-y-8">
      <header>
        <h1 className="flex items-center gap-2 text-2xl font-semibold text-gray-900">
          <Settings2 className="h-6 w-6 text-primary-600" />
          Integrações & credenciais
        </h1>
        <p className="mt-1 text-sm text-gray-600">
          Tokens e chaves dos provedores externos. Tudo é gravado de forma cifrada e nunca exibido de
          volta — preencha um campo apenas para substituir o valor atual.
        </p>
      </header>

      <section className="space-y-3">
        <h2 className="text-sm font-semibold uppercase tracking-wide text-gray-500">
          Provedores de login (OAuth)
        </h2>
        {credenciais.isError ? (
          <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
            {extrairMensagemDeErro(credenciais.error)}
          </div>
        ) : null}
        {credenciais.isLoading ? (
          <div className="flex items-center gap-2 text-sm text-gray-500">
            <Loader2 className="h-4 w-4 animate-spin" /> Carregando…
          </div>
        ) : null}
        <div className="grid grid-cols-1 gap-3 lg:grid-cols-2">
          {credsOauth.map((c) => (
            <CredencialOAuthCard key={c.provedor} cred={c} />
          ))}
        </div>
      </section>

      <section className="space-y-3">
        <h2 className="text-sm font-semibold uppercase tracking-wide text-gray-500">
          Armazenamento de arquivos (exames digitalizados)
        </h2>
        <div className="grid grid-cols-1 gap-3 lg:grid-cols-2">
          <DigitalOceanSpacesCard cred={credSpaces} />
        </div>
      </section>

      <section className="space-y-3">
        <h2 className="text-sm font-semibold uppercase tracking-wide text-gray-500">
          Consulta de pacientes (SISREG)
        </h2>
        <div className="grid grid-cols-1 gap-3 lg:grid-cols-2">
          <SisregCard cred={credSisreg} />
        </div>
      </section>

      <section className="space-y-3">
        <h2 className="text-sm font-semibold uppercase tracking-wide text-gray-500">
          Transporte de pacientes (TFD)
        </h2>
        <div className="grid grid-cols-1 gap-3 lg:grid-cols-2">
          <GoogleMapsCard />
          <NavigationSdkCard />
          <WhatsAppCard />
        </div>
      </section>

      {podeVerProxy ? (
        <>
          <ProxyServicoSection
            servico="cpf"
            titulo="Proxy CPF (validação na Receita)"
            descricao="Motores tentados em cadeia: se um falhar (timeout/indisponível), cai para o próximo antes de devolver erro."
          />
          <ProxyServicoSection
            servico="cep"
            titulo="Proxy CEP (busca de endereço)"
            descricao="Mesma lógica de fallback dos motores, agora para a consulta de CEP."
          />
        </>
      ) : null}

      {podeVerIa || podeVerSisreg || podeVerTokens ? (
        <section className="space-y-3">
          <h2 className="text-sm font-semibold uppercase tracking-wide text-gray-500">
            Outras configurações
          </h2>
          <div className="grid grid-cols-1 gap-3 lg:grid-cols-2">
            {podeVerIa ? (
              <LinkCard
                to="/app/ia/configuracao"
                titulo="Inteligência (Claude / Anthropic)"
                descricao="Token do provedor de IA e bases consultáveis."
              />
            ) : null}
            {podeVerSisreg ? (
              <LinkCard
                to="/app/sisreg/configuracao"
                titulo="SISREG / DATASUS"
                descricao="Credenciais e escopo do feed de regulação."
              />
            ) : null}
            {podeVerTokens ? (
              <LinkCard
                to="/app/api-tokens"
                titulo="API Tokens"
                descricao="Chaves de serviço geradas por nós (mostradas uma única vez)."
              />
            ) : null}
          </div>
        </section>
      ) : null}

      <p className="flex items-center gap-1.5 text-xs text-gray-400">
        <Sparkles className="h-3.5 w-3.5" />
        Os provedores de login (Microsoft/Facebook/Google) alimentam o login social do app do cidadão.
        <KeySquare className="ml-2 h-3.5 w-3.5" />
        Segredos cifrados em repouso (Data Protection).
      </p>
    </div>
  );
}
