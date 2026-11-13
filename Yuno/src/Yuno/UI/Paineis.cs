namespace Yuno.UI
{
    // Um painel por botão da barra de ferramentas "YUNO". Cada um mostra só a sua função.

    public class PainelDefinir : MainDocker
    {
        public PainelDefinir(object app) : base(app) { MostrarSomente(0, "Montagem", "defina os moldes por tamanho e parte", "d42ad130-bfe3-5425-a89c-8eb049c96e6c"); }
    }

    public class PainelAplicar : MainDocker
    {
        public PainelAplicar(object app) : base(app) { MostrarSomente(1, "Aplicar matrizes", "copia a arte da matriz para os tamanhos", "02edb4fb-53e0-5dc4-aef8-366e55479653"); }
    }

    public class PainelPedido : MainDocker
    {
        public PainelPedido(object app) : base(app) { MostrarSomente(1, "Pedido", "leia a planilha e monte as peças do pedido", "ae35b949-ced2-5ac3-ac17-2e2801a0f9e1"); }
    }

    public class PainelOS : MainDocker
    {
        public PainelOS(object app) : base(app) { MostrarSomente(2, "OS", "gere a ordem de serviço do pedido", "f8340c7e-e692-55aa-9477-1d93603b9a90"); }
    }

    public class PainelFerramentas : MainDocker
    {
        public PainelFerramentas(object app) : base(app) { MostrarSomente(3, "Ferramentas", "transferir, cópias, alinhar, cores, limpar", "700ff718-0858-5b4c-b5ba-9922ff67be8b"); }
    }

    public class PainelEncaixe : MainDocker
    {
        public PainelEncaixe(object app) : base(app) { MostrarSomente(4, "Encaixe sobre outro", "coloca as peças sobre o encaixe do nesting", "d388a882-7133-5d1c-8ebf-29dfdc2a1229"); }
    }

    public class PainelExportar : MainDocker
    {
        public PainelExportar(object app) : base(app) { MostrarSomente(3, "Exportar 50%", "exporte o pedido em PDF reduzido", "dfa57b98-d259-5419-aea5-18bbafb5cc91"); }
    }

    public class PainelConfig : MainDocker
    {
        public PainelConfig(object app) : base(app) { MostrarSomente(6, "Configurações", "tamanhos, planilha e ID", "f8340c7e-e692-55aa-9477-1d93603b9a90"); }
    }
}
