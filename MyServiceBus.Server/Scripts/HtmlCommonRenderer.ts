class HtmlCommonRenderer{
    public static renderGraph(c: number[], showValue: (v:number)=>string) {
        const max = Utils.getMax(c);

        const w = 50;

        let coef = max == 0 ? 0 : w / max;

        let result =
            '<svg width="240" height="' +
            w +
            '"> <rect width="240" height="' +
            w +
            '" style="fill:none;stroke-width:;stroke:black" />';

        let i = 0;
        for (let m of c) {
            let y = w - m * coef;

            result +=
                '<line x1="' +
                i +
                '" y1="' +
                w +
                '" x2="' +
                i +
                '" y2="' +
                y +
                '" style="stroke:lightblue;stroke-width:2" />';
            i += 2;
        }

        return result + '<text x="0" y="15" fill="red">' + showValue(max) + "</text></svg>";
    }
    
    public static RenderQueueSlices(queueSlices: IQueueIndex[]):string{
        let result ="";
        for (let c of queueSlices) {
            result += c.from + "-" + c.to + "; ";
        }
        
        return result;
    }
    
    private static getTopicsTable():string{
        return '<table class="table table-striped">' +
            '<tr><th>Topic</th><th>Topic connections</th><th>Queues</th><tbody id="topicData"></tbody></tr>'+
            '</table>'
    }

    private static getTcpConnectionsDataTable():string{
        return '<table class="table table-striped">' +
            '<tr><th>Id</th><th>Info</th><th>Topics</th><th>Queues</th><tbody id="tcpConnections"></tbody></tr>'+
            '</table>'
    }
    
    
    public static getMainLayout():string{
        return this.getTopicsTable()+this.getTcpConnectionsDataTable();
    }
}