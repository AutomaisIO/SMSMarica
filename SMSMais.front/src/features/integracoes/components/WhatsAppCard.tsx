import { useEffect, useState } from 'react';
import { ChevronDown, Loader2, MessageCircle } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { usePermissao } from '@/shared/auth/authStore';
import { Button } from '@/shared/ui/Button';
import { Campo } from '@/shared/ui/Campo';
import { Input } from '@/shared/ui/Input';
import { useSalvarTfdWhatsApp, useTfdWhatsApp } from '@/features/integracoes/api';

/**
 * Conexão de WhatsApp da instância.
 *
 * Quem fala com a Meta é o Automais.Zap — token do System User, App Secret, verify token e
 * WABA ficaram lá. Esta tela cuida só do que é nosso: por onde falamos com o relay e por qual
 * linha enviamos.
 *
 * Não há credencial da Meta nesta instância — nem na tela, nem no banco.
 */
export function WhatsAppCard() {
  const podeEditar = usePermissao('IntegracoesConfig', 'Edicao');
  const config = useTfdWhatsApp();
  const salvar = useSalvarTfdWhatsApp();

  const [aberto, setAberto] = useState(false);
  const [phoneNumberId, setPhoneNumberId] = useState('');
  const [ativo, setAtivo] = useState(true);
  const [zapBaseUrl, setZapBaseUrl] = useState('');
  const [zapToken, setZapToken] = useState('');
  const [zapSegredoWebhook, setZapSegredoWebhook] = useState('');
  const [zapAtivo, setZapAtivo] = useState(false);
  const [erro, setErro] = useState<string | null>(null);
  const [salvo, setSalvo] = useState(false);

  useEffect(() => {
    if (config.data) {
      setPhoneNumberId(config.data.phoneNumberId ?? '');
      setAtivo(config.data.ativo);
      setZapBaseUrl(config.data.zapBaseUrl ?? '');
      setZapAtivo(config.data.zapAtivo);
    }
  }, [config.data]);

  const pronto = (config.data?.zapTokenConfigurado ?? false) && (config.data?.zapAtivo ?? false);

  function aoSalvar(e: React.FormEvent) {
    e.preventDefault();
    setErro(null);
    setSalvo(false);
    // Campo omitido = mantido como está no banco. É assim que as credenciais legadas da Meta
    // sobrevivem a um salvamento desta tela, mesmo sem aparecer nela.
    salvar.mutate(
      {
        phoneNumberId: phoneNumberId.trim() || null,
        ativo,
        zapBaseUrl: zapBaseUrl.trim() || null,
        zapToken: zapToken || undefined,
        zapSegredoWebhook: zapSegredoWebhook || undefined,
        zapAtivo,
      },
      {
        onSuccess: () => {
          setSalvo(true);
          setZapToken('');
          setZapSegredoWebhook('');
        },
        onError: (err) => setErro(extrairMensagemDeErro(err)),
      },
    );
  }

  return (
    <div className="rounded-xl border border-gray-200 bg-white p-4 shadow-sm">
      <button
        type="button"
        onClick={() => setAberto((a) => !a)}
        className="flex w-full items-center justify-between gap-3 text-left"
      >
        <span className="flex items-center gap-2">
          <MessageCircle className="h-5 w-5 text-primary-600" />
          <span className="font-medium text-gray-900">WhatsApp</span>
        </span>
        <span className="flex items-center gap-2">
          <span
            className={
              pronto && (config.data?.ativo ?? false)
                ? 'rounded-full bg-green-100 px-2 py-0.5 text-[11px] font-medium text-green-700'
                : 'rounded-full bg-gray-100 px-2 py-0.5 text-[11px] font-medium text-gray-500'
            }
          >
            {pronto ? (config.data?.ativo ? 'Conectado' : 'Desligado') : 'Não configurado'}
          </span>
          <ChevronDown className={`h-4 w-4 text-gray-400 transition-transform ${aberto ? 'rotate-180' : ''}`} />
        </span>
      </button>

      {aberto ? (
        <form onSubmit={aoSalvar} className="mt-4 space-y-5 border-t border-gray-100 pt-4">
          {/* A chave-geral vem primeiro e sozinha: desligá-la derruba receber E enviar, e o
              sintoma no envio é silencioso (cai em modo simulado). Já derrubou uma vez por
              parecer irmã da chave que só escolhe o transporte. */}
          <div className="rounded-lg border border-amber-200 bg-amber-50 px-3 py-2.5">
            <label className="flex items-start gap-2 text-sm text-gray-800">
              <input
                type="checkbox"
                className="mt-0.5"
                checked={ativo}
                onChange={(e) => setAtivo(e.target.checked)}
                disabled={!podeEditar}
              />
              <span>
                <span className="font-medium">WhatsApp habilitado</span>
                <span className="mt-0.5 block text-xs text-amber-800">
                  Chave geral do canal. Desligada, o sistema para de <strong>receber</strong> e de{' '}
                  <strong>enviar</strong> mensagem — e o envio falha em silêncio.
                </span>
              </span>
            </label>
          </div>

          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
            <Campo label="URL do Automais.Zap" htmlFor="zap-url">
              <Input
                id="zap-url"
                value={zapBaseUrl}
                onChange={(e) => setZapBaseUrl(e.target.value)}
                placeholder="https://api.smsmais.automais.com"
                disabled={!podeEditar}
              />
            </Campo>

            <Campo
              label="Linha de envio (phone_number_id)"
              htmlFor="wa-phone"
              dica="Identifica por qual número as mensagens saem."
            >
              <Input
                id="wa-phone"
                value={phoneNumberId}
                onChange={(e) => setPhoneNumberId(e.target.value)}
                disabled={!podeEditar}
              />
            </Campo>

            <Campo
              label="Token do tenant"
              htmlFor="zap-token"
              dica={
                config.data?.zapTokenConfigurado
                  ? 'Já configurado — preencha apenas para substituir.'
                  : 'Gerado no painel do Automais.Zap, na tela do tenant.'
              }
            >
              <Input
                id="zap-token"
                type="password"
                value={zapToken}
                onChange={(e) => setZapToken(e.target.value)}
                placeholder={config.data?.zapTokenConfigurado ? '••••••••' : 'zap_...'}
                autoComplete="new-password"
                disabled={!podeEditar}
              />
            </Campo>

            <Campo
              label="Segredo do webhook"
              htmlFor="zap-segredo"
              dica={
                config.data?.zapSegredoWebhookConfigurado
                  ? 'Já configurado.'
                  : 'Gerado no painel do Automais.Zap, na tela do WABA.'
              }
            >
              <Input
                id="zap-segredo"
                type="password"
                value={zapSegredoWebhook}
                onChange={(e) => setZapSegredoWebhook(e.target.value)}
                placeholder={config.data?.zapSegredoWebhookConfigurado ? '••••••••' : 'gerado no painel do Zap'}
                autoComplete="new-password"
                disabled={!podeEditar}
              />
            </Campo>
          </div>

          <div>
            <label className="flex items-center gap-2 text-sm text-gray-700">
              <input
                type="checkbox"
                checked={zapAtivo}
                onChange={(e) => setZapAtivo(e.target.checked)}
                disabled={!podeEditar}
              />
              Enviar pelo Automais.Zap
            </label>
            <p className="mt-1 text-xs text-gray-500">
              Reservado para o dia em que houver outro transporte. Hoje o Automais.Zap é o único
              caminho de envio — desmarcar não tem para onde voltar.
            </p>
          </div>

          {erro ? (
            <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">{erro}</div>
          ) : null}

          {podeEditar ? (
            <div className="flex flex-wrap items-center justify-end gap-3">
              {salvo ? <span className="text-sm text-green-600">Salvo.</span> : null}
              <Button type="submit" disabled={salvar.isPending}>
                {salvar.isPending ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : null}
                Salvar
              </Button>
            </div>
          ) : (
            <p className="text-xs text-gray-500">Você não tem permissão para editar.</p>
          )}
        </form>
      ) : null}
    </div>
  );
}
