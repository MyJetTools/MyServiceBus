var TopicQueueRenderer = /** @class */ (function () {
    function TopicQueueRenderer() {
    }
    TopicQueueRenderer.renderQueues = function (queues) {
        var itm = "";
        for (var _i = 0, queues_1 = queues; _i < queues_1.length; _i++) {
            var q = queues_1[_i];
            //itm += this.renderTopicQueue(q);
        }
        return itm;
    };
    /*
    private static renderTopicQueue(q: ITopicQueue){
        let deleteOnDisconnectBadge = q.deleteOnDisconnect
            ? '<span class="badge badge-success">auto-delete</span>'
            : '<span class="badge badge-warning">permanent</span>';

        let connectsBadge =
            q.connections == 0
                ? '<span class="badge badge-danger">Connects:' +
                q.connections +
                "</span>"
                : '<span class="badge badge-primary">Connects:' +
                q.connections +
                "</span>";

        let sizeBadge =
            q.queueSize < 1000
                ? '<span class="badge badge-success">size:' + q.queueSize + "</span>"
                : '<span class="badge badge-danger">size:' + q.queueSize + "</span>";

        let readSlicesBadge = '<span class="badge badge-success">QReady:'+HtmlCommonRenderer.RenderQueueSlices(q.readySlices)+'</span>';

        let leasedAmount = '<span class="badge badge-warning">Leased:'+q.leasedAmount+'</span>';

        return  '<table style="width:100%"><tr><td style="width: 100%">'+
            "<div>"+q.queueId+"<span> </span>" +sizeBadge + "<span> </span>" +deleteOnDisconnectBadge +"</div>"+
            connectsBadge +
            "<span> </span>" +
            readSlicesBadge +
            "<span> </span>" +
            leasedAmount +
            "</td><td>"+HtmlCommonRenderer.renderGraph(q.executionDuration, v => this.toDuration(v))+"</td></tr></table>";
    }
*/
    TopicQueueRenderer.toDuration = function (v) {
        return (v / 1000).toFixed(3) + "ms";
    };
    TopicQueueRenderer.renderTopicQueues = function (queues) {
        var result = "";
        for (var _i = 0, queues_2 = queues; _i < queues_2.length; _i++) {
            var queue = queues_2[_i];
            result += this.renderTopicQueue(queue);
        }
        return result;
    };
    TopicQueueRenderer.renderTopicQueue = function (queue) {
        var queueTypeBadge = queue.deleteOnDisconnect
            ? HtmlCommonRenderer.renderBadge('success', 'auto-delete')
            : HtmlCommonRenderer.renderBadge('warning', 'permanent');
        return '<table style="width: 100%"><tr>' +
            '<th style="width: 100%">' + queue.id + ' ' + queueTypeBadge + '</th>' +
            '<th style="width: 100%"></th>' +
            '</tr></table>';
    };
    return TopicQueueRenderer;
}());
//# sourceMappingURL=TopicQueueRenderer.js.map