var HtmlTopicQueueRenderer = /** @class */ (function () {
    function HtmlTopicQueueRenderer() {
    }
    HtmlTopicQueueRenderer.renderTopicQueues = function (topicId, queues) {
        var result = "";
        for (var _i = 0, queues_1 = queues; _i < queues_1.length; _i++) {
            var queue = queues_1[_i];
            result += this.renderTopicQueue(topicId, queue);
        }
        return result;
    };
    HtmlTopicQueueRenderer.renderTopicFirstLine = function (queue) {
        return queue.connections > 0
            ? HtmlCommonRenderer.renderBadge('primary', '<img style="width: 10px" src="/images/plug.svg"> ' + queue.connections)
            : HtmlCommonRenderer.renderBadge('danger', '<img style="width: 10px" src="/images/plug.svg"> ' + queue.connections);
    };
    HtmlTopicQueueRenderer.renderTopicSecondLine = function (queue) {
        var queueTypeBadge = queue.deleteOnDisconnect
            ? HtmlCommonRenderer.renderBadge('success', 'auto-delete')
            : HtmlCommonRenderer.renderBadge('warning', 'permanent');
        var sizeBadge = HtmlCommonRenderer.renderBadge(queue.size > 100 ? 'danger' : 'success', "Size:" + queue.size);
        var queueSize = Utils.getQueueSize(queue.ready);
        var queueBadge = HtmlCommonRenderer.renderBadge(queueSize > 1000 ? "danger" : "success", HtmlCommonRenderer.RenderQueueSlices(queue.ready));
        return queueBadge + ' ' + sizeBadge + ' ' + queueTypeBadge;
    };
    HtmlTopicQueueRenderer.renderTopicQueue = function (topicId, queue) {
        var topicQueueId = topicId + '-' + queue.id;
        return '<table style="width: 100%"><tr>' +
            '<td style="width: 100%">' + queue.id + ' <div id="queue1-' + topicQueueId + '">' + this.renderTopicFirstLine(queue) + '</div>' +
            '<div id="queue2-' + topicQueueId + '">' + this.renderTopicSecondLine(queue) + '</div></td>' +
            '<td style="width: 100%"><div style="font-size: 8px">Avg Event execution duration</div><div id="queue-duration-graph-' + topicQueueId + '"></div></td>' +
            '</tr></table>';
    };
    return HtmlTopicQueueRenderer;
}());
//# sourceMappingURL=HtmlTopicQueueRenderer.js.map