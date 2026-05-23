import { useEffect, useState, type FormEvent } from 'react';
import { Lock } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { Button } from '@/shared/ui/Button';
import { Campo } from '@/shared/ui/Campo';
import { Input } from '@/shared/ui/Input';
import {
  enderecoVazio,
  FormularioEndereco,
  paraPayload,
  type EnderecoForm,
} from '@/shared/ui/FormularioEndereco';
import { UploadFoto } from '@/shared/ui/UploadFoto';
import { useAtualizarMinhaConta, useMeuPerfil } from '@/features/usuarios/api/queries';

function formatarCpf(cpf: string | null | undefined): string {
  if (!cpf) return '—';
  const d = cpf.replace(/\D/g, '');
  if (d.length !== 11) return cpf;
  return `${d.slice(0, 3)}.${d.slice(3, 6)}.${d.slice(6, 9)}-${d.slice(9)}`;
}

function formatarData(iso: string | null | undefined): string {
  if (!iso) return '—';
  const [a, m, d] = iso.split('-');
  if (!a || !m || !d) return iso;
  return `${d}/${m}/${a}`;
}

export function MeuPerfilPage() {
  const meu = useMeuPerfil();
  const salvar = useAtualizarMinhaConta();
  const [telefone, setTelefone] = useState('');
  const [endereco, setEndereco] = useState<EnderecoForm>(enderecoVazio);
  const [fotoBase64, setFotoBase64] = useState<string | null>(null);
  const [erroGlobal, setErroGlobal] = useState<string | null>(null);
  const [sucesso, setSucesso] = useState(false);

  useEffect(() => {
    if (!meu.data) return;
    const e = meu.data.endereco;
    setTelefone(meu.data.telefone ?? '');
    setFotoBase64(meu.data.fotoBase64 ?? null);
    setEndereco(
      e
        ? {
            cep: e.cep ?? '',
            logradouro: e.logradouro ?? '',
            numero: e.numero ?? '',
            complemento: e.complemento ?? '',
            bairro: e.bairro ?? '',
            cidade: e.cidade ?? '',
            uf: e.uf ?? '',
            pontoReferencia: e.pontoReferencia ?? '',
          }
        : enderecoVazio,
    );
  }, [meu.data]);

  async function aoEnviar(e: FormEvent) {
    e.preventDefault();
    setErroGlobal(null);
    setSucesso(false);
    const enderecoForm = paraPayload(endereco);
    const enderecoPayload = enderecoForm
      ? {
          cep: enderecoForm.cep,
          logradouro: enderecoForm.logradouro,
          numero: enderecoForm.numero || null,
          complemento: enderecoForm.complemento || null,
          bairro: enderecoForm.bairro,
          cidade: enderecoForm.cidade,
          uf: enderecoForm.uf,
          pontoReferencia: null,
        }
      : null;
    try {
      await salvar.mutateAsync({
        telefone: telefone || undefined,
        endereco: enderecoPayload,
        fotoBase64,
      });
      setSucesso(true);
    } catch (e) {
      setErroGlobal(extrairMensagemDeErro(e));
    }
  }

  if (meu.isLoading) return <div className="text-sm text-gray-500">Carregando…</div>;
  if (meu.isError || !meu.data)
    return (
      <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
        {extrairMensagemDeErro(meu.error)}
      </div>
    );

  const u = meu.data;

  return (
    <div className="space-y-6">
      <header>
        <h1 className="text-2xl font-semibold text-gray-900">Meu perfil</h1>
        <p className="mt-1 text-sm text-gray-600">
          Você pode atualizar a foto, o telefone e o endereço. Nome, CPF e data de nascimento são
          dados imutáveis — fale com um administrador se precisar corrigir.
        </p>
      </header>

      <form onSubmit={aoEnviar} className="space-y-6">
        <UploadFoto
          valor={fotoBase64}
          aoMudar={setFotoBase64}
          nome={u.nomeCompleto}
          desabilitado={salvar.isPending}
        />

        <section className="grid grid-cols-1 gap-4 md:grid-cols-2">
          <Campo label="Nome completo" htmlFor="nome" dica={<span className="inline-flex items-center gap-1"><Lock className="h-3 w-3" /> Imutável</span>}>
            <Input id="nome" value={u.nomeCompleto} disabled readOnly />
          </Campo>
          <Campo label="E-mail" htmlFor="email" dica={<span className="inline-flex items-center gap-1"><Lock className="h-3 w-3" /> Imutável</span>}>
            <Input id="email" value={u.email} disabled readOnly />
          </Campo>
          <Campo label="CPF" htmlFor="cpf" dica={<span className="inline-flex items-center gap-1"><Lock className="h-3 w-3" /> Imutável</span>}>
            <Input id="cpf" value={formatarCpf(u.cpf)} disabled readOnly />
          </Campo>
          <Campo label="Data de nascimento" htmlFor="nasc" dica={<span className="inline-flex items-center gap-1"><Lock className="h-3 w-3" /> Imutável</span>}>
            <Input id="nasc" value={formatarData(u.dataNascimento)} disabled readOnly />
          </Campo>

          <Campo label="Telefone" htmlFor="telefone" className="md:col-span-2">
            <Input
              id="telefone"
              value={telefone}
              onChange={(e) => setTelefone(e.target.value)}
              placeholder="(21) 99999-0000"
              disabled={salvar.isPending}
            />
          </Campo>
        </section>

        <section>
          <h2 className="mb-3 text-sm font-semibold text-gray-900">Endereço</h2>
          <FormularioEndereco
            valor={endereco}
            aoMudar={setEndereco}
            desabilitado={salvar.isPending}
            mostrarPontoReferencia={false}
          />
        </section>

        {erroGlobal ? (
          <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
            {erroGlobal}
          </div>
        ) : null}
        {sucesso ? (
          <div className="rounded-md border border-emerald-200 bg-emerald-50 px-3 py-2 text-sm text-emerald-700">
            Perfil atualizado.
          </div>
        ) : null}

        <div className="flex justify-end">
          <Button type="submit" disabled={salvar.isPending}>
            {salvar.isPending ? 'Salvando…' : 'Salvar alterações'}
          </Button>
        </div>
      </form>
    </div>
  );
}
