import { useEffect, useState } from 'react';
import { Headset, Trash2 } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { usePermissao } from '@/shared/auth/authStore';
import { Button } from '@/shared/ui/Button';
import { Campo } from '@/shared/ui/Campo';
import { ConfirmDialog } from '@/shared/ui/ConfirmDialog';
import { Input } from '@/shared/ui/Input';
import {
  useDefinirSoftphone,
  useRamaisLivres,
  useRemoverSoftphone,
  useSoftphoneDoUsuario,
} from '@/features/telefonia/api/queries';

type Props = {
  usuarioId: string;
  /** Nome do usuário, usado como sugestão do nome de exibição. */
  nomeSugerido: string;
};

/**
 * Aba "Softphone" da edição do usuário: habilita o telefone no navegador, escolhe o ramal e o
 * nome que aparece para quem recebe. Salva sozinha (não depende do "Salvar alterações" do
 * formulário), porque a mudança vai para a VM de telefonia na hora.
 */
export function SoftphoneSecao({ usuarioId, nomeSugerido }: Props) {
  const podeEditar = usePermissao('Telefonia', 'Edicao');
  const podeExcluir = usePermissao('Telefonia', 'Exclusao');
  const atual = useSoftphoneDoUsuario(usuarioId);
  const definir = useDefinirSoftphone();
  const remover = useRemoverSoftphone();

  const habilitado = !!atual.data?.habilitado;
  const [querHabilitar, setQuerHabilitar] = useState(false);
  const mostrarFormulario = habilitado || querHabilitar;
  const livres = useRamaisLivres(mostrarFormulario && podeEditar);

  const [ramal, setRamal] = useState('');
  const [nome, setNome] = useState('');
  const [ativo, setAtivo] = useState(true);
  const [erro, setErro] = useState<string | null>(null);
  const [salvo, setSalvo] = useState(false);
  const [confirmarRemocao, setConfirmarRemocao] = useState(false);

  // Carrega o que está gravado; sem softphone, sugere o 1º número livre e o nome do usuário.
  useEffect(() => {
    if (!atual.data) return;
    if (atual.data.habilitado) {
      setRamal(atual.data.ramal ?? '');
      setNome(atual.data.nomeExibicao ?? '');
      setAtivo(atual.data.ativo);
    } else {
      setNome((n) => n || nomeSugerido);
    }
  }, [atual.data, nomeSugerido]);

  useEffect(() => {
    if (!habilitado && !ramal && livres.data?.sugestoes[0]) setRamal(livres.data.sugestoes[0]);
  }, [habilitado, ramal, livres.data]);

  async function salvar() {
    setErro(null);
    setSalvo(false);
    try {
      await definir.mutateAsync({ usuarioId, payload: { ramal: ramal.trim(), nomeExibicao: nome.trim(), ativo } });
      setQuerHabilitar(false);
      setSalvo(true);
    } catch (e) {
      setErro(extrairMensagemDeErro(e));
    }
  }

  async function confirmarRemover() {
    setErro(null);
    try {
      await remover.mutateAsync(usuarioId);
      setConfirmarRemocao(false);
      setRamal('');
      setSalvo(false);
    } catch (e) {
      setConfirmarRemocao(false);
      setErro(extrairMensagemDeErro(e));
    }
  }

  const faixa =
    livres.data?.inicio != null && livres.data?.fim != null ? `Faixa de softphone: ${livres.data.inicio}–${livres.data.fim}.` : null;

  return (
    <section className="space-y-4">
      <div>
        <h3 className="mb-1 flex items-center gap-2 text-sm font-semibold text-gray-900">
          <Headset className="h-4 w-4" /> Softphone
        </h3>
        <p className="text-xs text-gray-500">
          Telefone dentro do navegador. Com o softphone habilitado, o usuário liga e recebe ligações pelo
          próprio painel, sem digitar senha: o ramal registra sozinho quando ele entra.
        </p>
      </div>

      {atual.isLoading ? <p className="text-sm text-gray-500">Carregando…</p> : null}

      {erro ? (
        <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">{erro}</div>
      ) : null}
      {salvo ? (
        <div className="rounded-md border border-green-200 bg-green-50 px-3 py-2 text-sm text-green-800">
          Softphone salvo. Vale a partir do próximo carregamento do painel pelo usuário.
        </div>
      ) : null}

      {!atual.isLoading && !mostrarFormulario ? (
        <div className="rounded-lg border border-dashed border-gray-300 p-4 text-sm text-gray-600">
          Este usuário não tem softphone.
          {podeEditar ? (
            <Button type="button" variante="outline" className="ml-3" onClick={() => setQuerHabilitar(true)}>
              Habilitar softphone
            </Button>
          ) : null}
        </div>
      ) : null}

      {mostrarFormulario ? (
        <div className="space-y-3 rounded-lg border border-gray-200 bg-gray-50 p-4">
          <div className="grid grid-cols-1 gap-3 md:grid-cols-3">
            <Campo
              label="Ramal"
              htmlFor="softphoneRamal"
              dica={
                <>
                  {faixa}
                  {livres.isError ? ' Não foi possível consultar os números livres.' : null}
                </>
              }
            >
              <Input
                id="softphoneRamal"
                list="softphoneRamaisLivres"
                inputMode="numeric"
                value={ramal}
                onChange={(e) => setRamal(e.target.value.replace(/\D/g, ''))}
                disabled={!podeEditar || definir.isPending}
              />
              <datalist id="softphoneRamaisLivres">
                {(livres.data?.sugestoes ?? []).map((n) => (
                  <option key={n} value={n} />
                ))}
              </datalist>
            </Campo>
            <Campo
              label="Nome de exibição"
              htmlFor="softphoneNome"
              className="md:col-span-2"
              dica="Aparece no visor de quem recebe a ligação."
            >
              <Input
                id="softphoneNome"
                value={nome}
                maxLength={60}
                onChange={(e) => setNome(e.target.value)}
                disabled={!podeEditar || definir.isPending}
              />
            </Campo>
          </div>

          <label className="flex items-center gap-2 text-sm text-gray-700">
            <input
              type="checkbox"
              checked={ativo}
              onChange={(e) => setAtivo(e.target.checked)}
              disabled={!podeEditar || definir.isPending}
            />
            <span>Ativo (desligado, o número continua reservado mas o softphone não liga)</span>
          </label>

          <div className="flex flex-wrap items-center justify-between gap-2 border-t border-gray-200 pt-3">
            {habilitado && podeExcluir ? (
              <Button type="button" variante="ghost" onClick={() => setConfirmarRemocao(true)} disabled={remover.isPending}>
                <Trash2 className="h-4 w-4" /> Remover softphone
              </Button>
            ) : (
              <span />
            )}
            <div className="flex gap-2">
              {!habilitado ? (
                <Button type="button" variante="ghost" onClick={() => setQuerHabilitar(false)}>
                  Cancelar
                </Button>
              ) : null}
              {podeEditar ? (
                <Button type="button" onClick={salvar} disabled={definir.isPending || !ramal || !nome.trim()}>
                  {definir.isPending ? 'Salvando…' : habilitado ? 'Salvar softphone' : 'Habilitar'}
                </Button>
              ) : null}
            </div>
          </div>
        </div>
      ) : null}

      <ConfirmDialog
        aberto={confirmarRemocao}
        titulo="Remover softphone"
        mensagem={`O ramal ${atual.data?.ramal ?? ''} deixa de existir e o número fica livre para outro usuário. Ligações em curso caem.`}
        rotuloConfirmar="Remover"
        destrutivo
        carregando={remover.isPending}
        aoConfirmar={confirmarRemover}
        aoCancelar={() => setConfirmarRemocao(false)}
      />
    </section>
  );
}
