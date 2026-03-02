// 生成唯一的 Session ID
function generateSessionId() {
    return 'session-' + Math.random().toString(36).substr(2, 9) + '-' + Date.now();
}

const sessionId = generateSessionId();
document.getElementById('sessionId').textContent = sessionId;

const messagesDiv = document.getElementById('messages');
const messageInput = document.getElementById('messageInput');
const sendBtn = document.getElementById('sendBtn');
const imageInput = document.getElementById('imageInput');
const imagePreview = document.getElementById('imagePreview');
const previewImg = document.getElementById('previewImg');
const removeImageBtn = document.getElementById('removeImageBtn');
const imageUploadBtn = document.querySelector('.image-upload-btn');

// 存儲選中的圖片 Base64 數據
let selectedImageBase64 = null;

// 添加消息到聊天框
function addMessage(sender, content, isBot = false, toolCalls = null, imageUrl = null) {
    const messageElement = document.createElement('div');
    messageElement.className = isBot ? 'message bot-message' : 'message user-message';
    
    const senderName = isBot ? 'AI 助理' : '你';
    let htmlContent = '<strong>' + senderName + '：</strong>';
    
    // 如果有圖片，顯示圖片
    if (!isBot && imageUrl) {
        htmlContent += '<div style="margin: 8px 0; border-radius: 8px; overflow: hidden; max-width: 150px;"><img src="' + imageUrl + '" alt="圖片" style="width: 100%; height: auto; display: block;"></div>';
    }
    
    htmlContent += '<p>' + escapeHtml(content) + '</p>';
    
    // 如果有工具呼叫信息，添加工具呼叫顯示
    if (isBot && toolCalls && toolCalls.length > 0) {
        htmlContent += '<div style="margin-top: 8px; padding-top: 8px; border-top: 1px solid rgba(0,0,0,0.1); font-size: 12px; opacity: 0.8;">';
        toolCalls.forEach(tool => {
            const status = tool.resultSuccess ? '✅' : '❌';
            htmlContent += '<div>' + status + ' 工具: <strong>' + tool.toolName + '</strong></div>';
            if (tool.result) {
                htmlContent += '<div style="margin-left: 12px; color: #666;">結果: ' + escapeHtml(tool.result) + '</div>';
            }
        });
        htmlContent += '</div>';
    }
    
    messageElement.innerHTML = htmlContent;
    messagesDiv.appendChild(messageElement);
    messagesDiv.scrollTop = messagesDiv.scrollHeight;
}

// 添加加載狀態
function showLoading() {
    const loadingElement = document.createElement('div');
    loadingElement.id = 'loading';
    loadingElement.className = 'loading';
    loadingElement.textContent = 'AI 助理正在思考中...';
    messagesDiv.appendChild(loadingElement);
    messagesDiv.scrollTop = messagesDiv.scrollHeight;
}

// 移除加載狀態
function removeLoading() {
    const loadingElement = document.getElementById('loading');
    if (loadingElement) {
        loadingElement.remove();
    }
}

// 逃脫 HTML 特殊字符
function escapeHtml(text) {
    const map = {
        '&': '&amp;',
        '<': '&lt;',
        '>': '&gt;',
        '"': '&quot;',
        "'": '&#039;'
    };
    return text.replace(/[&<>"']/g, m => map[m]);
}

// 圖片選擇處理
imageInput.addEventListener('change', (event) => {
    const file = event.target.files[0];
    if (!file) {
        return;
    }

    // 驗證檔案類型
    if (!file.type.startsWith('image/')) {
        alert('請選擇圖片檔案');
        imageInput.value = '';
        return;
    }

    // 驗證檔案大小（5MB 限制）
    if (file.size > 5 * 1024 * 1024) {
        alert('圖片大小不能超過 5MB');
        imageInput.value = '';
        return;
    }

    // 使用 FileReader 將圖片轉換為 Base64
    const reader = new FileReader();
    reader.onload = (e) => {
        selectedImageBase64 = e.target.result;
        previewImg.src = selectedImageBase64;
        imagePreview.style.display = 'flex';
    };
    reader.readAsDataURL(file);
});

// 移除圖片
removeImageBtn.addEventListener('click', () => {
    selectedImageBase64 = null;
    imageInput.value = '';
    imagePreview.style.display = 'none';
    previewImg.src = '';
});

// 發送消息
async function sendMessage() {
    const message = messageInput.value.trim();
    if (!message && !selectedImageBase64) {
        return;
    }

    // 在發送前保存圖片數據，然後立即清空所有狀態
    const imageToSend = selectedImageBase64;

    // 顯示用戶消息
    addMessage('user', message || '（已上傳圖片）', false, null, imageToSend);

    // 立即清空UI和狀態，防止視覺重複
    messageInput.value = '';
    selectedImageBase64 = null;
    imageInput.value = '';
    imagePreview.style.display = 'none';
    previewImg.src = '';
    sendBtn.disabled = true;

    // 顯示加載狀態
    showLoading();

    try {
        const requestBody = {
            sessionId: sessionId,
            message: message || '請分析這張圖片'
        };

        // 如果有圖片，添加到請求中
        if (imageToSend) {
            requestBody.imageBase64 = imageToSend;
        }

        const response = await fetch('/api/chat', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
            },
            body: JSON.stringify(requestBody)
        });

        removeLoading();

        if (!response.ok) {
            addMessage('bot', '錯誤：' + response.status, true);
            sendBtn.disabled = false;
            return;
        }

        const data = await response.json();

        if (data.success) {
            addMessage('bot', data.content, true, data.toolCalls);
        } else {
            addMessage('bot', '執行失敗：' + (data.error || '未知錯誤'), true);
        }
    } catch (error) {
        removeLoading();
        addMessage('bot', '網絡錯誤：' + error.message, true);
    } finally {
        sendBtn.disabled = false;
        messageInput.focus();
    }
}

// 事件監聽
sendBtn.addEventListener('click', sendMessage);
messageInput.addEventListener('keypress', (event) => {
    if (event.key === 'Enter' && !event.shiftKey) {
        event.preventDefault();
        sendMessage();
    }
});

// 圖片上傳按鈕點擊
imageUploadBtn.addEventListener('click', () => {
    imageInput.click();
});

// 頁面加載完後自動聚焦到輸入框
document.addEventListener('DOMContentLoaded', () => {
    messageInput.focus();
});
