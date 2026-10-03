import type { ReactNode } from 'react';
import { apiBaseAbsoluto } from '@/shared/api/httpClient';
import { instituicao } from '@/shared/tema/instituicao';
import { BrandLogo } from '@/shared/ui/BrandLogo';

/**
 * Política de privacidade da extensão do Chrome — página PÚBLICA (sem login), porque a Chrome
 * Web Store exige um endereço aberto. Nome da secretaria, contato e endereço da API vêm da
 * identidade da instância (`/publico/instituicao`, ADR-0043): nada institucional fixo aqui.
 *
 * O texto descreve o pacote de PRODUÇÃO (SMSMais.chrome/prod). Mudou o que a extensão coleta?
 * Esta página e `SMSMais.chrome/loja/LOJA.md` mudam no mesmo commit — a loja confere uma com a
 * outra.
 */
const ATUALIZADA_EM = '03/10/2026';

export function PoliticaPrivacidadeExtensaoPage() {
  const i = instituicao();
  const responsavel = i.nomeSecretaria || i.nome;
  const contato = i.emailDpo ?? i.emailContato;

  return (
    <div className="min-h-screen bg-gray-50 px-4 py-8 sm:px-6 lg:px-8">
      <div className="mx-auto w-full max-w-3xl">
        <div className="flex justify-center">
          <BrandLogo className="h-16 w-auto max-w-[260px]" />
        </div>
        <article className="card mt-8 space-y-6 px-5 py-8 text-sm leading-relaxed text-gray-700 shadow-lg sm:px-10">
          <header>
            <h1 className="text-xl font-semibold text-gray-900">Política de privacidade</h1>
            <p className="mt-1 text-gray-500">Extensão do Chrome “SMSMais — Ponte de Sistemas”</p>
          </header>

          <p>
            A extensão SMSMais — Ponte de Sistemas é uma ferramenta interna de <strong>{responsavel}</strong>,
            usada pelas equipes da rede de saúde que operam o SISREG e os prontuários eletrônicos da rede.
          </p>

          <Secao titulo="O que a extensão coleta">
            Quando o operador agenda ou cancela uma solicitação no SISREG, a extensão registra no SMSMais: o
            comando (agendou ou cancelou), o número da solicitação, a data e a hora e o nome do operador do
            SISREG exibido na tela. Para funcionar, ela também guarda no navegador o token da sessão do painel
            do SMSMais e um identificador aleatório da instalação.
          </Secao>

          <Secao titulo="O que a extensão não coleta">
            Nome, CNS, CPF, telefone, endereço ou qualquer outro dado do paciente; senhas; o conteúdo das telas
            do SISREG ou dos prontuários; o histórico de navegação.
          </Secao>

          <Secao titulo="Para que os dados servem">
            Para registrar no SMSMais os agendamentos e os cancelamentos feitos no SISREG, com o operador
            responsável, e para o assistente de agenda dos prontuários consultar as escalas do SISREG já
            registradas no SMSMais.
          </Secao>

          <Secao titulo="Para onde vão">
            Só para a API do SMSMais da própria instituição (<span className="break-all">{apiBaseAbsoluto}</span>),
            por conexão cifrada (HTTPS). Não são vendidos, compartilhados com terceiros nem usados para
            publicidade.
          </Secao>

          <Secao titulo="Por quanto tempo">
            No navegador, a sessão vale até expirar (8 horas), até o logout no painel ou até a API recusá-la. No
            SMSMais, os registros seguem as regras de guarda de {responsavel}, conforme a Lei Geral de Proteção
            de Dados (Lei 13.709/2018).
          </Secao>

          <Secao titulo="Contato">
            {contato ? (
              <>
                Dúvidas sobre esta política ou sobre os seus dados:{' '}
                <a className="text-primary-700 underline" href={`mailto:${contato}`}>
                  {contato}
                </a>
                {i.telefone ? <> · {i.telefone}</> : null}.
              </>
            ) : (
              <>Dúvidas sobre esta política ou sobre os seus dados: procure a ouvidoria de {responsavel}.</>
            )}
          </Secao>

          <p className="border-t border-gray-100 pt-4 text-xs text-gray-400">Atualizada em {ATUALIZADA_EM}.</p>
        </article>
      </div>
    </div>
  );
}

function Secao({ titulo, children }: { titulo: string; children: ReactNode }) {
  return (
    <section>
      <h2 className="mb-1 text-base font-semibold text-gray-900">{titulo}</h2>
      <p>{children}</p>
    </section>
  );
}
